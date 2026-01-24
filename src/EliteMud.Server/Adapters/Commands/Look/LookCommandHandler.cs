using EliteMud.Application.Commands.Look;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Look;

[Command("look", Aliases = new[] { "l" })]
internal sealed class LookCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly IScriptEngine _scriptEngine;
    private readonly LookHandler _lookHandler;
    private readonly ConnectionRegistry _connectionRegistry;

    public LookCommandHandler(IWorldState worldState, IScriptEngine scriptEngine, ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _scriptEngine = scriptEngine;
        _connectionRegistry = connectionRegistry;
        _lookHandler = new LookHandler(worldState, () => _connectionRegistry.GetConnections().Select(c => c.Player));
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Position checks (legacy: act.informative.c:661-667)
        // Must be at least Resting to look around
        if (context.Player.Position < Position.Sleeping)
        {
            await context.Session.SendLineAsync("You can't see anything but stars!", cancellationToken);
            return CommandOutcome.Continue;
        }
        else if (context.Player.Position == Position.Sleeping)
        {
            await context.Session.SendLineAsync("You can't see anything, you're sleeping!", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // If a target is specified
        if (!string.IsNullOrWhiteSpace(command.Argument))
        {
            // Check for "look in <container>" syntax (legacy: act.informative.c:749-783)
            var parts = command.Argument.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0].Equals("in", StringComparison.OrdinalIgnoreCase))
            {
                // Handle "look in <container>"
                await HandleLookInContainer(context, parts[1], cancellationToken);
                return CommandOutcome.Continue;
            }

            // Otherwise, examine the object/mob/player (look <target>)
            var result = _lookHandler.HandleLookAt(context.Player, command.Argument);
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Otherwise, show the room
        var view = _lookHandler.Handle(context.Player);
        
        // Room name with color
        await context.Session.SendLineAsync($"#C{view.Name}#N", cancellationToken);
        
        // Room description - trim leading newline/whitespace and send as-is
        // (it already contains embedded newlines, so we use SendAsync to avoid adding extra)
        var description = view.Description.TrimStart('\n', '\r').TrimStart();
        await context.Session.SendAsync(description, cancellationToken);
        
        // Ensure description ends with newline before showing objects/mobs/exits
        if (!description.EndsWith('\n'))
        {
            await context.Session.SendAsync("\r\n", cancellationToken);
        }
        
        // Objects (green color)
        foreach (var line in view.ObjectLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }
        
        // NPCs (yellow color)
        foreach (var line in view.MobLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        // Other players (cyan color)
        foreach (var line in view.PlayerLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        // Exits line
        await context.Session.SendLineAsync(view.ExitLine, cancellationToken);
        
        var room = _worldState.World.GetRoom(context.Player.RoomId);
        await context.ExecuteScriptHookAsync(_scriptEngine, ScriptHook.OnLook, room, null, cancellationToken);
        
        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Handle "look in <container>" command.
    /// Legacy: act.informative.c:749-786
    /// </summary>
    private async ValueTask HandleLookInContainer(
        ConnectionContext context,
        string containerName,
        CancellationToken cancellationToken)
    {
        // Find container in inventory or room
        var container = FindContainer(context.Player, containerName);
        
        if (container is null)
        {
            await context.Session.SendLineAsync($"You don't see {GetArticle(containerName)} {containerName} here.", cancellationToken);
            return;
        }

        // Check if it's a container
        if (!container.Definition.Type.Equals("container", StringComparison.OrdinalIgnoreCase))
        {
            await context.Session.SendLineAsync("That is not a container.", cancellationToken);
            return;
        }

        // Show container name and location
        var location = IsInInventory(context.Player, container) ? "(carried)" : "(here)";
        await context.Session.SendLineAsync($"{container.Definition.ShortDescription} {location}:", cancellationToken);

        // List contents
        if (container.Contents.Count == 0)
        {
            await context.Session.SendLineAsync("  (empty)", cancellationToken);
        }
        else
        {
            foreach (var item in container.Contents)
            {
                await context.Session.SendLineAsync($"  {item.Definition.ShortDescription}", cancellationToken);
            }
        }
    }

    private ObjectInstance? FindContainer(PlayerState player, string containerName)
    {
        // Parse "2.corpse" style targeting
        var (index, name) = TargetParser.ParseTarget(containerName);
        if (index == 0)
            return null; // Invalid format (e.g., "abc.corpse")
        
        // Check inventory first
        var inventory = _worldState.GetPlayerInventory(player);
        var inventoryMatch = TargetParser.FindNthMatch(inventory, name, index);
        if (inventoryMatch != null)
            return inventoryMatch;

        // Check room
        var room = _worldState.World.GetRoom(player.RoomId);
        var roomObjects = _worldState.GetObjectsInRoom(room.Id);
        return TargetParser.FindNthMatch(roomObjects, name, index);
    }

    private bool IsInInventory(PlayerState player, ObjectInstance obj)
    {
        return player.InventoryObjectIds.Contains(obj.InstanceId);
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }

    private static string GetArticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "a";
        char first = char.ToLower(word[0]);
        return (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u') ? "an" : "a";
    }
}

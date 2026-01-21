using EliteMud.Application.Commands.Get;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Get;

internal sealed class GetCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly GetHandler _getHandler;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public GetCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _getHandler = new GetHandler(worldState);
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Get;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var argument = command.Argument ?? string.Empty;
        
        // Check for "get <item> all.X" pattern (e.g., "get all all.corpse")
        // Legacy: act.obj1.c:803-824 (FIND_ALLDOT case)
        if (await TryHandleAllDotPattern(argument, context, cancellationToken))
        {
            return CommandOutcome.Continue;
        }
        
        var result = _getHandler.Handle(context.Player, argument);
        
        if (!result.Success)
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // If there's a custom message (like "get all"), send it directly
        if (!string.IsNullOrEmpty(result.Message))
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Success with object - use ActMessage to broadcast
        if (result.Object is not null)
        {
            if (result.ContainerName is not null)
            {
                // Getting from container: "You get $p from the corpse."
                await context.ActToCharAsync(
                    _actService,
                    $"You get $p from the {result.ContainerName}.",
                    obj: result.Object,
                    cancellationToken: cancellationToken);

                await context.ActToNotCharAsync(
                    _actService,
                    _connectionRegistry,
                    $"$n gets $p from the {result.ContainerName}.",
                    obj: result.Object,
                    cancellationToken: cancellationToken);
            }
            else
            {
                // Getting from room: "You get $p."
                await context.ActToCharAsync(
                    _actService,
                    "You get $p.",
                    obj: result.Object,
                    cancellationToken: cancellationToken);

                await context.ActToNotCharAsync(
                    _actService,
                    _connectionRegistry,
                    "$n gets $p.",
                    obj: result.Object,
                    cancellationToken: cancellationToken);
            }
        }

        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Handle "get <item> all.X" pattern (e.g., "get all all.corpse").
    /// Loops through each matching container and gets items from it.
    /// Legacy: act.obj1.c:803-824 (FIND_ALLDOT case in do_get)
    /// </summary>
    private async ValueTask<bool> TryHandleAllDotPattern(
        string argument,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Parse "get <item> <container>"
        var parts = argument.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false; // Not a container get command
        
        var itemName = parts[0];
        var containerName = parts[1];
        
        // Check if containerName is "all.X" pattern
        var (index, name) = TargetParser.ParseTarget(containerName);
        if (index != -1) // Not an "all.X" pattern
            return false;
        
        // Find all matching containers in inventory first, then room
        var inventory = _worldState.GetPlayerInventory(context.Player);
        var room = _worldState.World.GetRoom(context.Player.RoomId);
        var roomObjects = _worldState.GetObjectsInRoom(room.Id);
        
        // Search inventory first (legacy order)
        var inventoryContainers = TargetParser.FindAllMatches(inventory, name)
            .Where(c => c.Definition.Type.Equals("container", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        // Then search room
        var roomContainers = TargetParser.FindAllMatches(roomObjects, name)
            .Where(c => c.Definition.Type.Equals("container", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var allContainers = inventoryContainers.Concat(roomContainers).ToList();
        
        if (allContainers.Count == 0)
        {
            await context.Session.SendLineAsync($"You can't seem to find any {name}s here.", cancellationToken);
            return true;
        }
        
        // Loop through each container and get items from it
        // Legacy does this by calling get_from_container for each match
        bool foundAny = false;
        
        foreach (var container in allContainers)
        {
            // Determine which objects to get from this container
            var itemsToGet = new List<ObjectInstance>();
            
            if (itemName.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Get all items from this container
                itemsToGet.AddRange(container.Contents);
            }
            else
            {
                // Get first matching item from this container
                var matchingItem = container.Contents.FirstOrDefault(item => 
                    MatchesTarget(item.Definition, itemName));
                if (matchingItem != null)
                {
                    itemsToGet.Add(matchingItem);
                }
            }
            
            // Transfer items and send messages
            foreach (var item in itemsToGet.ToList())
            {
                if (container.RemoveItem(item))
                {
                    context.Player.AddToInventory(item.InstanceId);
                    foundAny = true;
                    
                    // Send message: "You get $p from the corpse."
                    await context.ActToCharAsync(
                        _actService,
                        $"You get $p from the {container.Definition.ShortDescription}.",
                        obj: item.Definition,
                        cancellationToken: cancellationToken);

                    await context.ActToNotCharAsync(
                        _actService,
                        _connectionRegistry,
                        $"$n gets $p from the {container.Definition.ShortDescription}.",
                        obj: item.Definition,
                        cancellationToken: cancellationToken);
                }
            }
            
            // Check for player corpse looting (value[3]=2)
            if (itemsToGet.Count > 0)
            {
                bool isPlayerCorpse = container.Definition.Values.Count > 3 && container.Definition.Values[3] == 2;
                if (isPlayerCorpse && !container.Definition.ShortDescription.Contains(context.Player.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[LOOT] {context.Player.Name} looting {container.Definition.ShortDescription}.");
                }
            }
        }
        
        if (!foundAny)
        {
            // None of the containers had the item
            await context.Session.SendLineAsync($"You can't find any {itemName} in the {name}s.", cancellationToken);
        }
        
        return true; // We handled the all.X pattern
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
}
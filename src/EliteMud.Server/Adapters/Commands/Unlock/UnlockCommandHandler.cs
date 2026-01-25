using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Unlock;

[Command("unlock")]
internal sealed class UnlockCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public UnlockCommandHandler(
        IWorldState worldState,
        ActMessageService actService,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var argument = command.Argument ?? string.Empty;

        if (string.IsNullOrWhiteSpace(argument))
        {
            await context.Session.SendLineAsync("Unlock what?", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Try to find a door first
        var doorResult = DoorFinder.FindDoor(_worldState, context.Player, argument);
        
        if (doorResult.Found && doorResult.Direction.HasValue && doorResult.Exit != null)
        {
            return await HandleDoorUnlock(context, doorResult.Direction.Value, doorResult.Exit, cancellationToken);
        }

        // Not a door, try to find a container in inventory or room
        var container = FindContainer(context.Player, argument);
        
        if (container is null)
        {
            // If we tried to find a door and it had an error message, use that
            if (doorResult.ErrorMessage != null)
            {
                await context.Session.SendLineAsync(doorResult.ErrorMessage, cancellationToken);
            }
            else
            {
                await context.Session.SendLineAsync($"You don't see that here.", cancellationToken);
            }
            return CommandOutcome.Continue;
        }

        // Check if it's actually a container
        if (container.Definition.Details?.Container is null)
        {
            await context.Session.SendLineAsync($"That's not a container.", cancellationToken);
            return CommandOutcome.Continue;
        }

        var containerDetails = container.Definition.Details.Container;

        // Check if container can be unlocked (has a key)
        if (containerDetails.KeyId < 0)
        {
            await context.Session.SendLineAsync($"That thing can't be unlocked.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if player has the key
        if (!HasKey(context.Player, containerDetails.KeyId))
        {
            await context.Session.SendLineAsync($"You don't seem to have the proper key.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if already unlocked
        if (!container.IsLocked)
        {
            await context.Session.SendLineAsync($"It is unlocked already.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Unlock the container
        container.IsLocked = false;

        await context.Session.SendLineAsync("*Click*", cancellationToken);

        // TODO: Notify room when act() system is fully implemented
        // Legacy: act("$n unlocks $p - 'click', it says.", FALSE, ch, obj, 0, TO_ROOM);

        return CommandOutcome.Continue;
    }
    
    /// <summary>
    /// Handle unlocking a door.
    /// Legacy: do_unlock() in act.movement.c for doors
    /// </summary>
    private async ValueTask<CommandOutcome> HandleDoorUnlock(
        ConnectionContext context,
        Direction direction,
        ExitDefinition exit,
        CancellationToken cancellationToken)
    {
        // Check if it's actually a door
        if (!exit.IsDoor)
        {
            await context.Session.SendLineAsync("That's absurd.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Get current door state
        var doorState = _worldState.GetDoorState(context.Player.RoomId, direction);
        if (doorState == null)
        {
            await context.Session.SendLineAsync("That's absurd.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if door has a keyhole
        if (exit.KeyId == null || exit.KeyId < 0)
        {
            await context.Session.SendLineAsync("There does not seem to be any keyholes.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if player has the key
        if (!HasKey(context.Player, exit.KeyId.Value))
        {
            await context.Session.SendLineAsync("You don't have the proper key.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if already unlocked
        if (!doorState.IsLocked)
        {
            await context.Session.SendLineAsync("It's already unlocked!", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Unlock the door (updates both sides)
        _worldState.SetDoorState(context.Player.RoomId, direction, isClosed: doorState.IsClosed, isLocked: false);
        
        // Send success message
        await context.Session.SendLineAsync("*Click*", cancellationToken);
        
        // TODO: Notify room when act() system is fully implemented
        // Legacy: act("$n unlocks the $F.", FALSE, ch, 0, EXIT(ch, door)->keyword, TO_ROOM);
        
        return CommandOutcome.Continue;
    }
    
    /// <summary>
    /// Check if player has a key with the specified object ID in their inventory.
    /// Legacy: has_key() function
    /// </summary>
    private bool HasKey(PlayerState player, int keyId)
    {
        if (keyId < 0) return false;
        
        var inventory = _worldState.GetPlayerInventory(player);
        return inventory.Any(obj => obj.Definition.Id == keyId);
    }

    /// <summary>
    /// Find a container in the player's inventory or room.
    /// Supports indexed targeting (e.g., "2.bag" for second bag).
    /// </summary>
    private ObjectInstance? FindContainer(PlayerState player, string containerName)
    {
        var (index, name) = TargetParser.ParseTarget(containerName);
        if (index == 0)
            return null; // Invalid format

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
}

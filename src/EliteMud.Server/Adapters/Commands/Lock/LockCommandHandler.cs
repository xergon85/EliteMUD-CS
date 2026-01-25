using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Lock;

[Command("lock")]
internal sealed class LockCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public LockCommandHandler(
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
            await context.Session.SendLineAsync("Lock what?", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Try to find a door first
        var doorResult = DoorFinder.FindDoor(_worldState, context.Player, argument);
        
        if (doorResult.Found && doorResult.Direction.HasValue && doorResult.Exit != null)
        {
            return await HandleDoorLock(context, doorResult.Direction.Value, doorResult.Exit, cancellationToken);
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

        // Check if container is closed first (must be closed before locking)
        if (!container.IsClosed)
        {
            await context.Session.SendLineAsync($"Maybe you should close it first...", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if container can be locked (has a key)
        if (containerDetails.KeyId < 0)
        {
            await context.Session.SendLineAsync($"That thing can't be locked.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if player has the key
        if (!HasKey(context.Player, containerDetails.KeyId))
        {
            await context.Session.SendLineAsync($"You don't seem to have the proper key.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if already locked
        if (container.IsLocked)
        {
            await context.Session.SendLineAsync($"It is locked already.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Lock the container
        container.IsLocked = true;

        await context.Session.SendLineAsync("*Cluck*", cancellationToken);

        // TODO: Notify room when act() system is fully implemented
        // Legacy: act("$n locks $p - 'cluck', it says.", FALSE, ch, obj, 0, TO_ROOM);

        return CommandOutcome.Continue;
    }
    
    /// <summary>
    /// Handle locking a door.
    /// Legacy: do_lock() in act.movement.c for doors
    /// </summary>
    private async ValueTask<CommandOutcome> HandleDoorLock(
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
        
        // Check if door is broken
        if (doorState.IsBroken)
        {
            await context.Session.SendLineAsync("The door is broken and cannot be locked.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if door is closed first
        if (!doorState.IsClosed)
        {
            await context.Session.SendLineAsync("You have to close it first, I'm afraid.", cancellationToken);
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
        
        // Check if already locked
        if (doorState.IsLocked)
        {
            await context.Session.SendLineAsync("It's already locked!", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Lock the door (updates both sides)
        _worldState.SetDoorState(context.Player.RoomId, direction, isClosed: true, isLocked: true);
        
        // Send success message
        await context.Session.SendLineAsync("*Click*", cancellationToken);
        
        // TODO: Notify room when act() system is fully implemented
        // Legacy: act("$n locks the $F.", FALSE, ch, 0, EXIT(ch, door)->keyword, TO_ROOM);
        
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

using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Close;

[Command("close")]
internal sealed class CloseCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public CloseCommandHandler(
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
            await context.Session.SendLineAsync("Close what?", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Try to find a door first
        var doorResult = DoorFinder.FindDoor(_worldState, context.Player, argument);
        
        if (doorResult.Found && doorResult.Direction.HasValue && doorResult.Exit != null)
        {
            return await HandleDoorClose(context, doorResult.Direction.Value, doorResult.Exit, cancellationToken);
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

        // Check if container is closeable
        if (!containerDetails.Flags.Contains("Closeable", StringComparer.OrdinalIgnoreCase))
        {
            await context.Session.SendLineAsync($"You can't close that.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if already closed
        if (container.IsClosed)
        {
            await context.Session.SendLineAsync($"It's already closed.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Close the container
        container.IsClosed = true;

        await context.Session.SendLineAsync(
            $"You close the {container.Definition.ShortDescription}.", 
            cancellationToken);

        // TODO: Notify room when act() system is fully implemented
        // Legacy: act("$n closes $p.", FALSE, ch, obj, 0, TO_ROOM);

        return CommandOutcome.Continue;
    }
    
    /// <summary>
    /// Handle closing a door.
    /// Legacy: do_gen_door() in act.movement.c with SCMD_CLOSE
    /// </summary>
    private async ValueTask<CommandOutcome> HandleDoorClose(
        ConnectionContext context,
        Direction direction,
        ExitDefinition exit,
        CancellationToken cancellationToken)
    {
        // Check if it's actually a door
        if (!exit.IsDoor)
        {
            await context.Session.SendLineAsync("That's not a door.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Get current door state
        var doorState = _worldState.GetDoorState(context.Player.RoomId, direction);
        if (doorState == null)
        {
            await context.Session.SendLineAsync("That's not a door.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if door is broken
        if (doorState.IsBroken)
        {
            await context.Session.SendLineAsync("The door is broken and cannot be closed.", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if already closed
        if (doorState.IsClosed)
        {
            await context.Session.SendLineAsync("It's already closed!", cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Close the door (updates both sides)
        _worldState.SetDoorState(context.Player.RoomId, direction, isClosed: true, isLocked: doorState.IsLocked);
        
        // Send success message
        await context.Session.SendLineAsync("Ok.", cancellationToken);
        
        // TODO: Notify room when act() system is fully implemented
        // Legacy: act("$n closes the $F $T.", FALSE, ch, 0, obj, TO_ROOM);
        
        return CommandOutcome.Continue;
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

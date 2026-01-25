using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Goto;

/// <summary>
/// Immortal command to teleport to any room by ID.
/// Legacy: do_goto in act.wizard.c
/// TODO: Add level restriction when immortal system is implemented
/// </summary>
[Command("goto")]
internal sealed class GotoCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly LookCommandHandler _lookHandler;

    public GotoCommandHandler(IWorldState worldState, LookCommandHandler lookHandler)
    {
        _worldState = worldState;
        _lookHandler = lookHandler;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Check if argument is provided
        if (string.IsNullOrWhiteSpace(command.Argument))
        {
            await context.Session.SendLineAsync("Usage: goto <room_id>", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Parse room ID
        if (!int.TryParse(command.Argument.Trim(), out var targetRoomId))
        {
            await context.Session.SendLineAsync("Invalid room ID. Must be a number.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if room exists
        if (!_worldState.World.Rooms.ContainsKey(targetRoomId))
        {
            await context.Session.SendLineAsync($"Room {targetRoomId} does not exist.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Move player to target room
        var oldRoomId = player.RoomId;
        player.RoomId = targetRoomId;

        // Show message
        await context.Session.SendLineAsync($"You teleport to room {targetRoomId}.", cancellationToken);

        // Auto-look at new room
        await _lookHandler.HandleAsync(
            new CommandRequest("look", null, null),
            context,
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

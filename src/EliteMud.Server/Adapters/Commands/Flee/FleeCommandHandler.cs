using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Flee;

internal sealed class FleeCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly Func<IEnumerable<ConnectionContext>> _connections;
    private readonly LookCommandHandler _lookHandler;
    private readonly Application.Commands.Flee.FleeService _fleeService;

    public FleeCommandHandler(
        IWorldState worldState,
        Func<IEnumerable<ConnectionContext>> connections,
        LookCommandHandler lookHandler,
        Application.Commands.Flee.FleeService fleeService)
    {
        _worldState = worldState;
        _connections = connections;
        _lookHandler = lookHandler;
        _fleeService = fleeService;
    }

    public CommandKind Kind => CommandKind.Flee;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Check if in bad position (legacy: act.offensive.c:388)
        if (player.Position < CombatService.POS_FIGHTING)
        {
            await context.Session.SendLineAsync(
                "You are in pretty bad shape, unable to flee!",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Get current room for stopping combat later
        var currentRoomId = player.RoomId;

        // Broadcast flee attempt to room (legacy: act.offensive.c:443)
        await BroadcastToRoomExceptAsync(
            context,
            $"{player.Name} panics, and attempts to flee.",
            cancellationToken);

        // Attempt to flee using FleeService
        var result = _fleeService.AttemptFlee(
            player,
            currentRoomId,
            () => _connections().Select(c => c.Player),
            () => _worldState.GetMobsInRoom(currentRoomId));

        if (!result.Success)
        {
            // No valid exits found (legacy: act.offensive.c:421)
            await context.Session.SendLineAsync("PANIC!  You couldn't escape!", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Apply the flee result (moves player, stops combat, applies XP loss)
        _fleeService.ApplyFleeResult(
            player,
            result,
            () => _connections().Select(c => c.Player),
            context.Id);

        // Send success message (legacy: act.offensive.c:454)
        await context.Session.SendLineAsync("You flee head over heels.", cancellationToken);

        // Show new room by looking (same as move command)
        await _lookHandler.HandleAsync(
            new CommandRequest(CommandKind.Look, null, null), 
            context, 
            cancellationToken);

        return CommandOutcome.Continue;
    }

    private async ValueTask BroadcastToRoomExceptAsync(
        ConnectionContext speaker,
        string message,
        CancellationToken cancellationToken)
    {
        foreach (var connection in _connections())
        {
            if (connection.Id == speaker.Id)
            {
                continue;
            }

            if (connection.Player.RoomId != speaker.Player.RoomId)
            {
                continue;
            }

            await connection.Session.SendLineAsync(message, cancellationToken);
        }
    }
}

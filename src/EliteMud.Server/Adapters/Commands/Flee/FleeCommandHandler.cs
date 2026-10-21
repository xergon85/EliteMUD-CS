using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Flee;

[Command("flee", Aliases = new[] { "f" })]
internal sealed class FleeCommandHandler : ICommandHandler
{
    private readonly ActMessenger _act;
    private readonly LookCommandHandler _lookHandler;
    private readonly FleeHandler _fleeHandler;

    public FleeCommandHandler(
        ActMessenger act,
        LookCommandHandler lookHandler,
        FleeHandler fleeHandler)
    {
        _act = act;
        _lookHandler = lookHandler;
        _fleeHandler = fleeHandler;
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions
        var validationResult = FleeCommandValidator.Validate(player);
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Get current room for stopping combat later
        var currentRoomId = player.RoomId;

        // Broadcast flee attempt to room (legacy: act.offensive.c:443)
        _act.BroadcastToRoom(player, $"{player.Name} panics, and attempts to flee.");

        // Attempt to flee using FleeService
        var result = _fleeHandler.AttemptFlee(player, currentRoomId);

        if (!result.Success)
        {
            // No valid exits found (legacy: act.offensive.c:421)
            await context.Session.SendLineAsync("PANIC!  You couldn't escape!", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Apply the flee result (moves player, stops combat, applies XP loss)
        var lossMessages = _fleeHandler.ApplyFleeResult(player, result);

        foreach (var line in lossMessages)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        // Send success message (legacy: act.offensive.c:454)
        await context.Session.SendLineAsync("You flee head over heels.", cancellationToken);

        // Show new room by looking (same as move command)
        await _lookHandler.HandleAsync(
            new CommandRequest("look", null, null),
            context,
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

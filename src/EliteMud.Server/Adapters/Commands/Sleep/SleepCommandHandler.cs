using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Sleep;

[Command("sleep", Aliases = new[] { "sleep", "sl" })]
internal sealed class SleepCommandHandler : ICommandHandler
{
    private readonly ActMessenger _act;

    public SleepCommandHandler(ActMessenger act)
    {
        _act = act;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate position change
        var validationResult = PositionChangeValidator.Validate(player, Position.Sleeping, "sleep");
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Change position
        player.Position = Position.Sleeping;

        // Send message to player
        await context.Session.SendLineAsync("You go to sleep.", cancellationToken);

        // Broadcast to room
        _act.BroadcastToRoom(context.Player, $"{player.Name} lies down and falls asleep.");

        return CommandOutcome.Continue;
    }
}

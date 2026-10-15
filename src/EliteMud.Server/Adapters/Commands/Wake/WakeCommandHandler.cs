using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wake;

[Command("wake", Aliases = new[] { "wake", "wa" })]
internal sealed class WakeCommandHandler : ICommandHandler
{
    private readonly ActMessenger _act;

    public WakeCommandHandler(ActMessenger act)
    {
        _act = act;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Wake has special validation (checks if already awake)
        var validationResult = PositionChangeValidator.ValidateWake(player);
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Change position to standing (wake always stands you up)
        player.Position = Position.Standing;

        // Send message to player
        await context.Session.SendLineAsync("You awaken and stand up.", cancellationToken);

        // Broadcast to room
        _act.BroadcastToRoom(context.Player, $"{player.Name} awakens.");

        return CommandOutcome.Continue;
    }
}

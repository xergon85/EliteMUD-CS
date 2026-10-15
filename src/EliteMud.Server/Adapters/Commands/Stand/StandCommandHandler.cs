using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Stand;

[Command("stand")]
internal sealed class StandCommandHandler : ICommandHandler
{
    private readonly ActMessenger _act;

    public StandCommandHandler(ActMessenger act)
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
        var validationResult = PositionChangeValidator.Validate(player, Position.Standing, "stand");
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Change position
        player.Position = Position.Standing;

        // Send message to player
        await context.Session.SendLineAsync("You stand up.", cancellationToken);

        _act.ToNotChar(player, "$n clambers to $s feet.");

        return CommandOutcome.Continue;
    }
}

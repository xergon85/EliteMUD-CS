using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Stand;

[Command("stand")]
internal sealed class StandCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public StandCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
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

        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} clambers to $s feet.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

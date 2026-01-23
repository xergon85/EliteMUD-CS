using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Rest;

[Command("rest")]
internal sealed class RestCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public RestCommandHandler(ConnectionRegistry connectionRegistry)
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
        var validationResult = PositionChangeValidator.Validate(player, Position.Resting, "rest");
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Change position
        player.Position = Position.Resting;

        // Send message to player
        await context.Session.SendLineAsync("You sit down and rest your tired bones.", cancellationToken);

        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} sits down and rests.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Rest;

internal sealed class RestCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public RestCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Rest;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions
        var validationResult = PositionChangeValidator.Validate(player, Position.Resting, "resting");
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        player.Position = Position.Resting;
        await context.Session.SendLineAsync("You sit down and rest.", cancellationToken);
        
        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} sits down and rests.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

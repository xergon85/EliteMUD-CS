using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Sit;

internal sealed class SitCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public SitCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Sit;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions
        var validationResult = PositionChangeValidator.Validate(player, Position.Sitting, "sitting");
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        player.Position = Position.Sitting;
        await context.Session.SendLineAsync("You sit down.", cancellationToken);
        
        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} sits down.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

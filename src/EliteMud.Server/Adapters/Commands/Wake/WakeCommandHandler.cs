using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Wake;

internal sealed class WakeCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public WakeCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Wake;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions (wake has special logic)
        var validationResult = PositionChangeValidator.ValidateWake(player);
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        player.Position = Position.Sitting; // Wake up to sitting, not standing
        await context.Session.SendLineAsync("You wake and sit up.", cancellationToken);
        
        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} awakens.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

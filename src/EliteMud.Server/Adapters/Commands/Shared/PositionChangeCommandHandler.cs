using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Configuration for position change commands.
/// </summary>
internal sealed record PositionChangeConfig(
    CommandKind Kind,
    Position TargetPosition,
    string PlayerMessage,
    string RoomMessage,
    bool UseWakeValidation = false);

/// <summary>
/// Generic handler for position change commands (stand, sit, rest, sleep, wake).
/// Consolidates duplicate logic across all position commands.
/// </summary>
internal sealed class PositionChangeCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly PositionChangeConfig _config;

    public PositionChangeCommandHandler(
        ConnectionRegistry connectionRegistry,
        PositionChangeConfig config)
    {
        _connectionRegistry = connectionRegistry;
        _config = config;
    }

    public CommandKind Kind => _config.Kind;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions (wake has special validation logic)
        var validationResult = _config.UseWakeValidation
            ? PositionChangeValidator.ValidateWake(player)
            : PositionChangeValidator.Validate(player, _config.TargetPosition, _config.TargetPosition.ToString().ToLower());

        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Change position
        player.Position = _config.TargetPosition;

        // Send message to player
        await context.Session.SendLineAsync(_config.PlayerMessage, cancellationToken);

        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            string.Format(_config.RoomMessage, player.Name),
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

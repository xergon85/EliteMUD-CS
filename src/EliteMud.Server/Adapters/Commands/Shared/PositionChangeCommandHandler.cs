using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Configuration for position change commands.
/// </summary>
internal sealed record PositionChangeConfig(
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
    private readonly ActMessenger _act;
    private readonly PositionChangeConfig _config;

    public PositionChangeCommandHandler(
        ActMessenger act,
        PositionChangeConfig config)
    {
        _act = act;
        _config = config;
    }
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
        _act.BroadcastToRoom(context.Player, string.Format(_config.RoomMessage, player.Name));

        return CommandOutcome.Continue;
    }
}

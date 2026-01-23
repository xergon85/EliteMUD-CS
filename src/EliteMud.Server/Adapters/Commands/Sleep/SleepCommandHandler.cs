using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Sleep;

[Command("sleep", Aliases = new[] { "sleep", "sl" })]
internal sealed class SleepCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public SleepCommandHandler(ConnectionRegistry connectionRegistry)
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
        var validationResult = PositionChangeValidator.Validate(player, Position.Sleeping, "sleep");
        var outcome = await context.HandleValidationAsync(validationResult, cancellationToken);
        if (outcome.HasValue) return outcome.Value;

        // Change position
        player.Position = Position.Sleeping;

        // Send message to player
        await context.Session.SendLineAsync("You go to sleep.", cancellationToken);

        // Broadcast to room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} lies down and falls asleep.",
            cancellationToken);

        return CommandOutcome.Continue;
    }
}

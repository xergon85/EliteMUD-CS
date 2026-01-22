using EliteMud.Application.Commands.PositionChange;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Sleep;

internal sealed class SleepCommandHandler : ICommandHandler
{
    private readonly ConnectionRegistry _connectionRegistry;

    public SleepCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public CommandKind Kind => CommandKind.Sleep;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest request,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Validate preconditions
        var validationResult = PositionChangeValidator.Validate(player, Position.Sleeping, "sleeping");
        if (!validationResult.IsValid)
        {
            await context.Session.SendLineAsync(validationResult.ErrorMessage!, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Set position to sleeping
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

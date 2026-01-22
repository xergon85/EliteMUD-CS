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
        var roomMessage = $"{player.Name} lies down and falls asleep.";
        var playersInRoom = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == player.RoomId && c.Id != context.Id);
        
        foreach (var observer in playersInRoom)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}

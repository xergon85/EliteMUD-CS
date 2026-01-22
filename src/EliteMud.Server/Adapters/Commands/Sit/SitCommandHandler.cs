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
        if (!validationResult.IsValid)
        {
            await context.Session.SendLineAsync(validationResult.ErrorMessage!, cancellationToken);
            return CommandOutcome.Continue;
        }

        player.Position = Position.Sitting;
        await context.Session.SendLineAsync("You sit down.", cancellationToken);
        
        var roomMessage = $"{player.Name} sits down.";
        var playersInRoom = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == player.RoomId && c.Id != context.Id);
        
        foreach (var observer in playersInRoom)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}

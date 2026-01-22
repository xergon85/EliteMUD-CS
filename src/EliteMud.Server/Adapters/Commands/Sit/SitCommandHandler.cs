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

        if (player.Position == Position.Sitting)
        {
            await context.Session.SendLineAsync("You are already sitting.", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (player.FightingConnectionId != null)
        {
            await context.Session.SendLineAsync("You can't sit while fighting!", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (player.Position < Position.Stunned)
        {
            await context.Session.SendLineAsync("You can't sit in your current state.", cancellationToken);
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

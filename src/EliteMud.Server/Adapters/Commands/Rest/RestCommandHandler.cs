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

        if (player.Position == Position.Resting)
        {
            await context.Session.SendLineAsync("You are already resting.", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (player.FightingConnectionId != null)
        {
            await context.Session.SendLineAsync("You can't rest while fighting!", cancellationToken);
            return CommandOutcome.Continue;
        }

        if (player.Position < Position.Stunned)
        {
            await context.Session.SendLineAsync("You can't rest in your current state.", cancellationToken);
            return CommandOutcome.Continue;
        }

        player.Position = Position.Resting;
        await context.Session.SendLineAsync("You sit down and rest.", cancellationToken);
        
        var roomMessage = $"{player.Name} sits down and rests.";
        var playersInRoom = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == player.RoomId && c.Id != context.Id);
        
        foreach (var observer in playersInRoom)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}

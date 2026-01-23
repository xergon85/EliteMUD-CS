using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Who;

[Command("who")]
internal sealed class WhoCommandHandler : ICommandHandler, IConnectionDirectory
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly WhoHandler _whoHandler;

    public WhoCommandHandler(ConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
        _whoHandler = new WhoHandler(this);
    }
    
    public IReadOnlyList<PlayerState> GetPlayers()
    {
        return _connectionRegistry.GetConnections()
            .Select(connection => connection.Player)
            .ToList();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _whoHandler.Handle();
        await context.Session.SendAsync(result.Message, cancellationToken);
        return CommandOutcome.Continue;
    }
}

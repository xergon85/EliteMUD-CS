using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
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
    
    public IReadOnlyList<string> GetPlayerNames()
    {
        return _connectionRegistry.GetConnections()
            .Select(connection => connection.Player.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _whoHandler.Handle();
        await context.Session.SendLineAsync("Players:", cancellationToken);
        foreach (var name in result.Names)
        {
            await context.Session.SendLineAsync($" - {name}", cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}

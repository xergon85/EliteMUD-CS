using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Who;

internal sealed class WhoCommandHandler : ICommandHandler, IConnectionDirectory
{
    private readonly Func<IEnumerable<ConnectionContext>> _connections;
    private readonly WhoHandler _whoHandler;

    public WhoCommandHandler(Func<IEnumerable<ConnectionContext>> connections)
    {
        _connections = connections;
        _whoHandler = new WhoHandler(this);
    }

    public CommandKind Kind => CommandKind.Who;

    public IReadOnlyList<string> GetPlayerNames()
    {
        return _connections()
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

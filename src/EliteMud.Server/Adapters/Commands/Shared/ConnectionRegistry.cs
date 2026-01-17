namespace EliteMud.Server.Adapters.Commands.Shared;

internal sealed class ConnectionRegistry
{
    private Func<IEnumerable<ConnectionContext>> _connections = () => Array.Empty<ConnectionContext>();

    public IEnumerable<ConnectionContext> GetConnections() => _connections();

    public void SetProvider(Func<IEnumerable<ConnectionContext>> provider)
    {
        _connections = provider;
    }
}

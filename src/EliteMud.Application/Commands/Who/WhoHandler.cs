using EliteMud.Application.Commands.Shared;

namespace EliteMud.Application.Commands.Who;

public sealed class WhoHandler
{
    private readonly IConnectionDirectory _connections;

    public WhoHandler(IConnectionDirectory connections)
    {
        _connections = connections;
    }

    public WhoResult Handle()
    {
        return new WhoResult(_connections.GetPlayerNames());
    }
}

using EliteMud.Game;

namespace EliteMud.Server;

internal sealed class ConnectionContext
{
    public ConnectionContext(int id, TelnetSession session, PlayerState player)
    {
        Id = id;
        Session = session;
        Player = player;
    }

    public int Id { get; }

    public TelnetSession Session { get; }

    public PlayerState Player { get; }
}

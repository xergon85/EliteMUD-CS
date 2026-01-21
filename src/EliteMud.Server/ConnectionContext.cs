using EliteMud.Game;

namespace EliteMud.Server;

internal sealed class ConnectionContext
{
    public ConnectionContext(int id, TelnetSession session, PlayerState player, int characterId)
    {
        Id = id;
        Session = session;
        Player = player;
        CharacterId = characterId;
    }

    public int Id { get; }

    public TelnetSession Session { get; }

    public PlayerState Player { get; }
    
    public int CharacterId { get; }
}

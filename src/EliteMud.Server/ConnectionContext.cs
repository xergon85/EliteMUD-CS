using EliteMud.Game;

namespace EliteMud.Server;

internal sealed class ConnectionContext
{
    public ConnectionContext(int id, TelnetSession session, PlayerState player, int characterId, string accountName)
    {
        Id = id;
        Session = session;
        Player = player;
        CharacterId = characterId;
        AccountName = accountName;
    }

    public int Id { get; }

    public TelnetSession Session { get; }

    public PlayerState Player { get; }
    
    public int CharacterId { get; }

    /// <summary>The account the player logged in with; admin rights are per account (<see cref="AdminPolicy"/>).</summary>
    public string AccountName { get; }
}

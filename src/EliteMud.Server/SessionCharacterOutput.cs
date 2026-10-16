using EliteMud.Application.Session;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server;

/// <summary>Sends a character's output down their Telnet session, if they have one.</summary>
internal sealed class SessionCharacterOutput : ICharacterOutput
{
    private readonly ConnectionRegistry _connections;

    public SessionCharacterOutput(ConnectionRegistry connections)
    {
        _connections = connections;
    }

    public bool IsConnected(ICombatant character) => _connections.Find(character) != null;

    public void Send(ICombatant character, string text) => _connections.Find(character)?.Session.Send(text);

    public void SendLine(ICombatant character, string text) => _connections.Find(character)?.Session.SendLine(text);
}

using EliteMud.Game;

namespace EliteMud.Application.Session;

/// <summary>
/// What a character is shown. The server sends it down the player's connection; a mob, or a player without one,
/// sees nothing, so callers need not check who they are sending to (#79). Sending only queues, so it never blocks.
/// </summary>
public interface ICharacterOutput
{
    /// <summary>Whether text sent to the character reaches anyone.</summary>
    bool IsConnected(ICombatant character);

    /// <summary>Text as it is, e.g. a prompt or a message that ends its own line.</summary>
    void Send(ICombatant character, string text);

    /// <summary>Text followed by a new line.</summary>
    void SendLine(ICombatant character, string text);
}

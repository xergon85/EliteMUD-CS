using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Sends <see cref="SkillMessage"/>s, the act messages an Application-layer combat result reports (skills, spells,
/// the damage path), to the players they're meant for. The actor can be a player or a mob: the room is the actor's.
/// </summary>
internal sealed class SkillMessageSender
{
    private readonly ActMessenger _act;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly LookCommandHandler _lookHandler;

    public SkillMessageSender(
        ActMessenger act,
        ConnectionRegistry connectionRegistry,
        LookCommandHandler lookHandler)
    {
        _act = act;
        _connectionRegistry = connectionRegistry;
        _lookHandler = lookHandler;
    }

    /// <summary>
    /// Send messages from any actor's action, e.g. a player's skill or a mob's hit in the combat tick, in order.
    /// </summary>
    public async Task SendAsync(
        IEnumerable<SkillMessage> messages,
        ICombatant actor,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await SendAsync(message, actor, cancellationToken);
        }
    }

    private async Task SendAsync(SkillMessage message, ICombatant actor, CancellationToken cancellationToken)
    {
        switch (message.Target)
        {
            case SkillMessageTarget.Actor:
                _act.ToChar(actor, message.Template, message.Victim);
                break;

            // Only a player victim has anyone to read it
            case SkillMessageTarget.Victim when message.Victim is ICombatant victim:
                _act.ToVict(actor, message.Template, victim);
                break;

            case SkillMessageTarget.Room:
                _act.ToRoom(actor, message.Template, message.Victim);
                break;

            case SkillMessageTarget.Others:
                _act.ToNotVict(actor, message.Template, message.Victim);
                break;

            // Looking needs the victim's connection: the look command replies down it
            case SkillMessageTarget.VictimLooks
                when message.Victim is ICombatant victim && _connectionRegistry.Find(victim) is { } victimConnection:
                await _lookHandler.HandleAsync(new CommandRequest("look", null, null), victimConnection,
                    cancellationToken);
                break;
        }
    }
}

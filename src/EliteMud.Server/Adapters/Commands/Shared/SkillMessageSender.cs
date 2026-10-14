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
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly LookCommandHandler _lookHandler;

    public SkillMessageSender(
        ActMessageService actService,
        ConnectionRegistry connectionRegistry,
        LookCommandHandler lookHandler)
    {
        _actService = actService;
        _connectionRegistry = connectionRegistry;
        _lookHandler = lookHandler;
    }

    /// <summary>
    /// Send messages from a connected player's action, in order.
    /// </summary>
    public Task SendAsync(
        IEnumerable<SkillMessage> messages,
        ConnectionContext context,
        CancellationToken cancellationToken) =>
        SendAsync(messages, context.Player, context, cancellationToken);

    /// <summary>
    /// Send messages from any actor's action, e.g. a mob's hit in the combat tick, in order.
    /// </summary>
    public Task SendAsync(
        IEnumerable<SkillMessage> messages,
        ICombatant actor,
        CancellationToken cancellationToken) =>
        SendAsync(messages, actor, ConnectionOf(actor), cancellationToken);

    private async Task SendAsync(
        IEnumerable<SkillMessage> messages,
        ICombatant actor,
        ConnectionContext? actorConnection,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await SendAsync(message, actor, actorConnection, cancellationToken);
        }
    }

    private async Task SendAsync(
        SkillMessage message,
        ICombatant actor,
        ConnectionContext? actorConnection,
        CancellationToken cancellationToken)
    {
        switch (message.Target)
        {
            case SkillMessageTarget.Actor when actorConnection != null:
                await SendToAsync(actorConnection, message, actor, cancellationToken);
                break;

            // Only a player victim has anyone to read it
            case SkillMessageTarget.Victim when ConnectionOf(message.Victim) is { } victimConnection:
                await SendToAsync(victimConnection, message, actor, cancellationToken);
                break;

            case SkillMessageTarget.Room:
                foreach (var connection in InRoomOf(actor))
                {
                    await SendToAsync(connection, message, actor, cancellationToken);
                }

                break;

            case SkillMessageTarget.Others:
                foreach (var connection in InRoomOf(actor)
                             .Where(c => c.Player != actor && c.Player != message.Victim))
                {
                    await SendToAsync(connection, message, actor, cancellationToken);
                }

                break;

            case SkillMessageTarget.VictimLooks when ConnectionOf(message.Victim) is { } victimConnection:
                await _lookHandler.HandleAsync(new CommandRequest("look", null, null), victimConnection,
                    cancellationToken);
                break;
        }
    }

    private ValueTask SendToAsync(
        ConnectionContext viewer,
        SkillMessage message,
        ICombatant actor,
        CancellationToken cancellationToken) =>
        viewer.Session.SendAsync(
            _actService.FormatMessage(message.Template, viewer.Player, actor, message.Victim),
            cancellationToken);

    private ConnectionContext? ConnectionOf(object? combatant) =>
        combatant is PlayerState player
            ? _connectionRegistry.GetConnections().FirstOrDefault(c => c.Player == player)
            : null;

    private IEnumerable<ConnectionContext> InRoomOf(ICombatant actor) =>
        _connectionRegistry.GetConnections().Where(c => c.Player.RoomId == actor.RoomId).ToList();
}

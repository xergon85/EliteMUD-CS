using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Sends <see cref="SkillMessage"/>s (act templates) to the actor, the victim, or the others in the room.
/// Used for everything an Application-layer combat result reports: skills, spells and the damage path.
/// </summary>
internal sealed class SkillMessageSender
{
    private readonly ActMessageService _actService;
    private readonly ConnectionRegistry _connectionRegistry;

    public SkillMessageSender(ActMessageService actService, ConnectionRegistry connectionRegistry)
    {
        _actService = actService;
        _connectionRegistry = connectionRegistry;
    }

    /// <summary>
    /// Send messages to the players they're meant for, in order.
    /// </summary>
    public async Task SendAsync(
        IEnumerable<SkillMessage> messages,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await SendMessageAsync(message, context, cancellationToken);
        }
    }

    /// <summary>
    /// Send a single skill message to appropriate target(s).
    /// </summary>
    private async Task SendMessageAsync(
        SkillMessage message,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var actor = context.Player;

        switch (message.Target)
        {
            case SkillMessageTarget.Actor:
                await SendToActorAsync(message.Template, actor, message.Victim, context, cancellationToken);
                break;

            case SkillMessageTarget.Victim:
                await SendToVictimAsync(message.Template, actor, message.Victim, cancellationToken);
                break;

            case SkillMessageTarget.Room:
                await SendToRoomAsync(message.Template, actor, message.Victim, cancellationToken);
                break;

            case SkillMessageTarget.Others:
                await SendToOthersAsync(message.Template, actor, message.Victim, context, cancellationToken);
                break;
        }
    }

    private async Task SendToActorAsync(
        string template,
        PlayerState actor,
        object? victim,
        ConnectionContext actorConnection,
        CancellationToken cancellationToken)
    {
        var formatted = _actService.FormatMessage(template, actor, actor, victim);
        await actorConnection.Session.SendLineAsync(formatted, cancellationToken);
    }

    private async Task SendToVictimAsync(
        string template,
        PlayerState actor,
        object? victim,
        CancellationToken cancellationToken)
    {
        if (victim is PlayerState victimPlayer)
        {
            var victimConnection = _connectionRegistry.GetConnections()
                .FirstOrDefault(c => c.Player.Id == victimPlayer.Id);

            if (victimConnection != null)
            {
                var formatted = _actService.FormatMessage(template, victimPlayer, actor, victimPlayer);
                await victimConnection.Session.SendLineAsync(formatted, cancellationToken);
            }
        }
    }

    private async Task SendToRoomAsync(
        string template,
        PlayerState actor,
        object? victim,
        CancellationToken cancellationToken)
    {
        var roomPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == actor.RoomId);

        foreach (var observer in roomPlayers)
        {
            var formatted = _actService.FormatMessage(template, observer.Player, actor, victim);
            await observer.Session.SendLineAsync(formatted, cancellationToken);
        }
    }

    private async Task SendToOthersAsync(
        string template,
        PlayerState actor,
        object? victim,
        ConnectionContext actorConnection,
        CancellationToken cancellationToken)
    {
        var victimConnectionId = victim is PlayerState victimPlayer
            ? _connectionRegistry.GetConnections().FirstOrDefault(c => c.Player.Id == victimPlayer.Id)?.Id
            : null;

        var observers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == actor.RoomId
                        && c.Id != actorConnection.Id
                        && (victimConnectionId == null || c.Id != victimConnectionId));

        foreach (var observer in observers)
        {
            var formatted = _actService.FormatMessage(template, observer.Player, actor, victim);
            await observer.Session.SendLineAsync(formatted, cancellationToken);
        }
    }
}

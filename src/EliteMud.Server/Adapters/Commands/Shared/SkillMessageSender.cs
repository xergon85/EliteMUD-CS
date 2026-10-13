using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Sends <see cref="SkillMessage"/>s, the act messages an Application-layer combat result reports (skills, spells,
/// the damage path), through the act() helpers in <see cref="ActMessageExtensions"/>.
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
            await SendAsync(message, context, cancellationToken);
        }
    }

    private Task SendAsync(SkillMessage message, ConnectionContext context, CancellationToken cancellationToken) =>
        message.Target switch
        {
            SkillMessageTarget.Actor => context.ActToCharAsync(
                _actService, message.Template, message.Victim, cancellationToken: cancellationToken),

            // Only a player victim has anyone to read it
            SkillMessageTarget.Victim when message.Victim is PlayerState victim => context.ActToVictAsync(
                _actService, _connectionRegistry, message.Template, victim, cancellationToken: cancellationToken),

            SkillMessageTarget.Room => context.ActToRoomAsync(
                _actService, _connectionRegistry, message.Template, message.Victim,
                cancellationToken: cancellationToken),

            SkillMessageTarget.Others => context.ActToNotVictAsync(
                _actService, _connectionRegistry, message.Template, message.Victim,
                cancellationToken: cancellationToken),

            _ => Task.CompletedTask
        };
}

using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Sends an act message to those who see it, each worded for its viewer (legacy act() in comm.c). The actor can be
/// a player or a mob: the viewers are the players in the actor's room. The victim ($N) can be either too.
/// </summary>
public sealed class ActMessenger
{
    private readonly ActMessageService _format;
    private readonly IWorldState _world;
    private readonly ICharacterOutput _output;

    public ActMessenger(ActMessageService format, IWorldState world, ICharacterOutput output)
    {
        _format = format;
        _world = world;
        _output = output;
    }

    /// <summary>To the actor.</summary>
    public void ToChar(
        ICombatant actor, string message, object? victim = null, ObjectDefinition? obj = null, string? textArg = null)
    {
        if (actor is PlayerState player)
        {
            Show(player, message, actor, victim, obj, textArg);
        }
    }

    /// <summary>To the victim.</summary>
    public void ToVict(
        ICombatant actor, string message, ICombatant victim, ObjectDefinition? obj = null, string? textArg = null)
    {
        if (victim is PlayerState player)
        {
            Show(player, message, actor, victim, obj, textArg);
        }
    }

    /// <summary>To everyone in the actor's room, the actor included.</summary>
    public void ToRoom(
        ICombatant actor, string message, object? victim = null, ObjectDefinition? obj = null, string? textArg = null)
    {
        foreach (var viewer in ViewersOf(actor))
        {
            Show(viewer, message, actor, victim, obj, textArg);
        }
    }

    /// <summary>To everyone in the actor's room but the actor.</summary>
    public void ToNotChar(
        ICombatant actor, string message, object? victim = null, ObjectDefinition? obj = null, string? textArg = null)
    {
        foreach (var viewer in ViewersOf(actor).Where(viewer => viewer != actor))
        {
            Show(viewer, message, actor, victim, obj, textArg);
        }
    }

    /// <summary>To everyone in the actor's room but the actor and the victim.</summary>
    public void ToNotVict(
        ICombatant actor, string message, object? victim = null, ObjectDefinition? obj = null, string? textArg = null)
    {
        foreach (var viewer in ViewersOf(actor).Where(viewer => viewer != actor && viewer != victim))
        {
            Show(viewer, message, actor, victim, obj, textArg);
        }
    }

    /// <summary>A line as it is, without substitutions, to everyone in the actor's room but the actor.</summary>
    public void BroadcastToRoom(ICombatant actor, string line)
    {
        foreach (var viewer in ViewersOf(actor).Where(viewer => viewer != actor))
        {
            _output.SendLine(viewer, line);
        }
    }

    private IReadOnlyList<PlayerState> ViewersOf(ICombatant actor) => _world.Population.PlayersIn(actor.RoomId);

    private void Show(
        PlayerState viewer, string message, ICombatant actor, object? victim, ObjectDefinition? obj, string? textArg) =>
        _output.Send(viewer, _format.FormatMessage(message, viewer, actor, victim, obj, textArg));
}

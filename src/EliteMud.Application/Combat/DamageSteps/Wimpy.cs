using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// A player hurt below their wimpy level flees right away, inside the hit (legacy damage() calls do_flee,
/// fight.c:995), so the attacker's next attack finds them gone. The flee's lines join the hit's messages, then the
/// victim sees the room they fled to. Not for hurting yourself (legacy victim != ch).
/// </summary>
public sealed class Wimpy : IDamageStep
{
    private readonly FleeHandler _fleeHandler;
    private readonly IWorldState _worldState;

    public Wimpy(FleeHandler fleeHandler, IWorldState worldState)
    {
        _fleeHandler = fleeHandler;
        _worldState = worldState;
    }

    public void Apply(DamageContext context)
    {
        if (context.SelfInflicted
            || context.Victim is not PlayerState victim
            || victim.Position <= Position.Stunned
            || victim.HitPoints >= victim.WimpyLevel)
        {
            return;
        }

        var messages = context.Messages;
        messages.Add(new SkillMessage(SkillMessageTarget.Victim, "You wimp out, and attempt to flee!", victim));

        var roomId = victim.RoomId;
        var result = _fleeHandler.AttemptFlee(victim, roomId, () => _worldState.GetMobsInRoom(roomId));
        if (!result.Success)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, "PANIC!  You couldn't escape!", victim));
            return;
        }

        // Legacy do_flee tells the room only once it has found a way out
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, "$N panics, and attempts to flee.", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, "$N panics, and attempts to flee.", victim));

        foreach (var line in _fleeHandler.ApplyFleeResult(victim, result))
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, line, victim));
        }

        messages.Add(new SkillMessage(SkillMessageTarget.Victim, "You flee head over heels.", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.VictimLooks, string.Empty, victim));
    }
}

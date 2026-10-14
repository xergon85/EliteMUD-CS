using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// What the victim and the room hear about the victim's state after the hit (legacy damage(), fight.c:953–979). The
/// death lines come with the kill. Legacy says "$n" from the victim's side to the room; here the attacker sends, so
/// the room line goes to the attacker and the others as "$N". Only for player victims: our mobs are alive or dead,
/// and their lines would reach nobody.
/// </summary>
public sealed class PositionMessages : IDamageStep
{
    private readonly EffectiveStats _stats;

    public PositionMessages(EffectiveStats stats)
    {
        _stats = stats;
    }

    public void Apply(DamageContext context)
    {
        if (context.Victim is not PlayerState victim)
        {
            return;
        }

        switch (victim.Position)
        {
            case Position.MortallyWounded:
                Tell(context, "$N is mortally wounded, and will die soon, if not aided.",
                    "You are mortally wounded, and will die soon, if not aided.");
                break;
            case Position.Incapacitated:
                Tell(context, "$N is incapacitated and will slowly die, if not aided.",
                    "You are incapacitated and will slowly die, if not aided.");
                break;
            case Position.Stunned:
                Tell(context, "$N is stunned, but will probably regain consciousness again.",
                    "You're stunned, but will probably regain consciousness again.");
                break;
            case > Position.Stunned:
                Wounds(context, victim);
                break;
        }
    }

    /// <summary>Both lines can come with one hit; legacy checks them separately, a miss included.</summary>
    private void Wounds(DamageContext context, PlayerState victim)
    {
        var maxHitPoints = _stats.Get(victim, AffectLocation.MaxHit);
        if (context.Damage > maxHitPoints / 5)
        {
            context.Messages.Add(new SkillMessage(SkillMessageTarget.Victim, "That Really did HURT!", victim));
        }

        if (victim.HitPoints < maxHitPoints / 5)
        {
            context.Messages.Add(new SkillMessage(
                SkillMessageTarget.Victim, "You wish that your wounds would stop BLEEDING so much!", victim));
        }
    }

    private static void Tell(DamageContext context, string toRoom, string toVictim)
    {
        if (!context.SelfInflicted)
        {
            context.Messages.Add(new SkillMessage(SkillMessageTarget.Actor, toRoom, context.Victim));
        }

        context.Messages.Add(new SkillMessage(SkillMessageTarget.Others, toRoom, context.Victim));
        context.Messages.Add(new SkillMessage(SkillMessageTarget.Victim, toVictim, context.Victim));
    }
}

using EliteMud.Application.Progression;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>The victim loses the hit points; the attacker gains experience for the damage (none between players).</summary>
public sealed class LoseHitPoints : IDamageStep
{
    private readonly CombatExperience _combatExperience;

    public LoseHitPoints(CombatExperience combatExperience)
    {
        _combatExperience = combatExperience;
    }

    public void Apply(DamageContext context)
    {
        context.Victim.HitPoints -= (short)context.Damage;
        _combatExperience.DamageDealt(context.Attacker, context.Victim, context.Damage);
    }
}

using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// Someone incapacitated or worse stops fighting (legacy damage(), fight.c:1026); the stunned fight on. Whoever was
/// fighting them keeps at it.
/// </summary>
public sealed class StopFightingBelowStunned : IDamageStep
{
    public void Apply(DamageContext context)
    {
        if (context.Victim.Position < Position.Stunned && context.Victim.Fighting != null)
        {
            CombatCalculator.StopFighting(context.Victim);
        }
    }
}

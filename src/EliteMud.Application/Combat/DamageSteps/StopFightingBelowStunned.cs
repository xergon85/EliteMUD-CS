using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// Someone incapacitated or worse stops fighting (legacy damage(), fight.c:1026); the stunned fight on. Whoever was
/// fighting them keeps at it.
/// </summary>
public sealed class StopFightingBelowStunned : IDamageStep
{
    private readonly CombatCalculator _combatCalculator;

    public StopFightingBelowStunned(CombatCalculator combatCalculator)
    {
        _combatCalculator = combatCalculator;
    }

    public void Apply(DamageContext context)
    {
        if (context.Victim.Position < Position.Stunned && context.Victim.Fighting != null)
        {
            _combatCalculator.StopFighting(context.Victim);
        }
    }
}

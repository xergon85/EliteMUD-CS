using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>Legacy set_fighting: each side that isn't fighting yet starts fighting the other. A miss counts.</summary>
public sealed class StartFighting : IDamageStep
{
    private readonly CombatCalculator _combatCalculator;

    public StartFighting(CombatCalculator combatCalculator)
    {
        _combatCalculator = combatCalculator;
    }

    public void Apply(DamageContext context)
    {
        if (context.SelfInflicted)
        {
            return;
        }

        if (context.Attacker.Fighting == null)
        {
            _combatCalculator.SetFighting(context.Attacker, context.Victim);
        }

        if (context.Victim.Fighting == null)
        {
            _combatCalculator.SetFighting(context.Victim, context.Attacker);
        }
    }
}

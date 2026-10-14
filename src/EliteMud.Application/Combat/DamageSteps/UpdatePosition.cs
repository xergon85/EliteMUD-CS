using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat.DamageSteps;

/// <summary>
/// Legacy update_pos: a player's position follows their hit points (stunned, incapacitated, mortally wounded, dead);
/// a mob is alive or dead. A miss never kills, even a mob already at 0 hit points or below.
/// </summary>
public sealed class UpdatePosition : IDamageStep
{
    private readonly CombatCalculator _combatCalculator;

    public UpdatePosition(CombatCalculator combatCalculator)
    {
        _combatCalculator = combatCalculator;
    }

    public void Apply(DamageContext context)
    {
        switch (context.Victim)
        {
            case PlayerState player:
                _combatCalculator.UpdatePosition(player);
                break;
            case MobInstance mob when context.Damage > 0 && mob.HitPoints <= 0:
                mob.Position = Position.Dead;
                break;
        }

        context.VictimDied = context.Damage > 0 && context.Victim.Position == Position.Dead;
    }
}

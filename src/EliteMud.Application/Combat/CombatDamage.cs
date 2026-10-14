using EliteMud.Application.Combat.DamageSteps;
using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Progression;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// The one place damage is dealt (legacy fight.c damage()). A damage source decides whether it hits and for how
/// much; everything after that happens here, as a fixed list of steps: fighting state, damage experience, and the
/// victim's death.
/// </summary>
public sealed class CombatDamage
{
    public CombatDamage(
        CombatCalculator combatCalculator,
        CombatExperience combatExperience,
        IWorldState worldState,
        IEnumerable<ICombatListener> listeners,
        FleeHandler fleeHandler,
        Func<IEnumerable<PlayerState>> onlinePlayers,
        EffectiveStats stats)
    {
        var listenerList = listeners.ToList();

        // Legacy damage() order: memory before death, the victim's state before the kill, kill experience before the
        // death penalty
        Steps = new IDamageStep[]
        {
            new IgnoreCorpse(),
            new StartFighting(combatCalculator),
            new NotifyHurt(listenerList),
            new CapDamage(),
            new LoseHitPoints(combatExperience),
            new UpdatePosition(combatCalculator),
            new PositionMessages(stats),
            new Wimpy(fleeHandler, worldState, onlinePlayers),
            new StopFightingBelowStunned(combatCalculator),
            new Kill(combatCalculator, combatExperience, listenerList),
            new PlayerDeath(combatExperience, worldState, stats),
            new MobDeath(worldState)
        };
    }

    /// <summary>The steps of one hit, in the order they run.</summary>
    public IReadOnlyList<IDamageStep> Steps { get; }

    /// <summary>
    /// Deal damage from a player or mob to a player or mob. A miss is damage 0: it still starts the fight.
    /// The messages cover what follows the source's own hit message (death, kill experience), in order.
    /// </summary>
    public DamageOutcome Apply(ICombatant attacker, ICombatant victim, int damage)
    {
        var context = new DamageContext(attacker, victim, damage);
        foreach (var step in Steps)
        {
            step.Apply(context);
            if (context.Stopped)
            {
                break;
            }
        }

        return new DamageOutcome(context.Damage, context.VictimDied, context.Messages);
    }
}

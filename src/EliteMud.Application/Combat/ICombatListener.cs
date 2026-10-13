using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// Something that reacts to combat without being part of it, e.g. mob memory. <see cref="CombatDamage"/> tells
/// every registered listener what happened; it doesn't know what they do with it. Implement only what you need.
/// </summary>
public interface ICombatListener
{
    /// <summary>The attacker damaged the victim (a miss counts: legacy damage() runs for 0 too).</summary>
    void Hurt(ICombatant attacker, ICombatant victim)
    {
    }

    /// <summary>The killer killed the victim, before the victim's death (corpse, penalty) is handled.</summary>
    void Killed(ICombatant killer, ICombatant victim)
    {
    }
}

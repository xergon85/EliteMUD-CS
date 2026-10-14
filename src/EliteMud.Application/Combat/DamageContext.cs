using EliteMud.Application.Skills;
using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// One hit on its way through <see cref="CombatDamage"/>'s steps: who hits whom, for how much, and what has happened
/// so far. Steps read and change it in order; none calls another.
/// </summary>
public sealed class DamageContext
{
    public DamageContext(ICombatant attacker, ICombatant victim, int damage)
    {
        Attacker = attacker;
        Victim = victim;
        Damage = damage;
    }

    public ICombatant Attacker { get; }

    public ICombatant Victim { get; }

    /// <summary>The damage to deal; steps may change it (e.g. the cap). 0 is a miss.</summary>
    public int Damage { get; set; }

    /// <summary>Set by a step that ends the hit: no later step runs.</summary>
    public bool Stopped { get; set; }

    public bool VictimDied { get; set; }

    /// <summary>What follows the source's own hit message, in order.</summary>
    public List<SkillMessage> Messages { get; } = new();

    /// <summary>Legacy ch == victim: no fight, no memory, no kill reward.</summary>
    public bool SelfInflicted => ReferenceEquals(Attacker, Victim);
}

using EliteMud.Game;

namespace EliteMud.Application.Progression;

/// <summary>
/// Experience from fighting, for players and mobs alike (legacy fight.c damage() and die()). Every damage source
/// (melee, skills, spells) calls the same methods, so the rules can't drift apart.
/// </summary>
public sealed class CombatExperience
{
    private const int ImmortalLevel = ExperienceService.MaxMortalLevel + 1;

    private static readonly ProgressionResult Nothing = new(Array.Empty<string>(), false);

    private readonly PlayerProgression _progression;
    private readonly ExperienceService _experience;

    public CombatExperience(PlayerProgression progression, ExperienceService experience)
    {
        _progression = progression;
        _experience = experience;
    }

    /// <summary>
    /// Experience for damage dealt: victim level × damage / 2. Not between two players, and not for immortals.
    /// Added directly, not through gain_exp, as legacy does: no cap, no message, and a level-up waits for the
    /// next real gain.
    /// </summary>
    public void DamageDealt(ICombatant attacker, ICombatant victim, int damage)
    {
        if (ReferenceEquals(attacker, victim)
            || damage <= 0
            || attacker.Level >= ImmortalLevel
            || (attacker is PlayerState && victim is PlayerState))
        {
            return;
        }

        attacker.Experience += victim.Level * damage / 2;
    }

    /// <summary>
    /// Experience for a kill: a quarter of the victim's experience, at least 1. Players gain through the normal
    /// rules (cap, messages, level-up); mobs gain capped on the content's mob XP curve, silently.
    /// Call before <see cref="Died"/>, so the killer's share comes from the victim's experience before the penalty.
    /// </summary>
    public ProgressionResult Killed(ICombatant killer, ICombatant victim)
    {
        var amount = Math.Max(1, victim.Experience / 4);
        if (killer is PlayerState player)
        {
            return _progression.GainExperience(player, amount);
        }

        killer.Experience += _experience.MobGain(killer.Level, amount);
        return Nothing;
    }

    /// <summary>The death penalty: half the experience needed for the next level, never below 0.</summary>
    public ProgressionResult Died(PlayerState victim) =>
        _progression.GainExperience(victim, -(_progression.ExpNeeded(victim) / 2));
}

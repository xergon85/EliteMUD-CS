using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Bash skill - shield attack that knocks opponent down.
/// Legacy: act.offensive.c:484-583 (do_bash)
/// 
/// Requirements:
/// - Must have shield equipped
/// - Success based on skill proficiency
/// 
/// Effects on success:
/// - 10 damage to victim
/// - Victim knocked to sitting position
/// - Victim gets 1 round WAIT_STATE
/// - Attacker gets 2 rounds WAIT_STATE
/// 
/// Effects on failure:
/// - 0 damage
/// - Attacker knocked to sitting position
/// - Attacker gets 2 rounds WAIT_STATE
/// </summary>
public sealed class BashSkill : ISkillHandler
{
    public SkillType SkillType => SkillType.Bash;

    public string Name => "Bash";

    public string Description => "A shield attack that knocks your opponent down";

    public int MinimumLevel => 1;

    public int WaitStateRounds => 2; // Attacker wait state (PULSE_VIOLENCE * 2)

    public bool CanUse(ICombatant user)
    {
        // Must have at least 1% proficiency
        if (user.GetSkill(SkillType.Bash) == 0)
        {
            return false;
        }

        // Must be at minimum level
        if (user.Level < MinimumLevel)
        {
            return false;
        }

        // Must be standing or fighting (not sitting/resting/sleeping/incapacitated)
        if (user.Position < Position.Fighting)
        {
            return false;
        }

        return true;
    }

    public string GetCannotUseMessage(ICombatant user)
    {
        if (user.GetSkill(SkillType.Bash) == 0)
        {
            return "You don't know how to bash!";
        }

        if (user.Level < MinimumLevel)
        {
            return $"You must be at least level {MinimumLevel} to bash.";
        }

        if (user.Position < Position.Fighting)
        {
            return "You can't bash while sitting down!";
        }

        return "You can't use that skill right now.";
    }

    /// <summary>
    /// Calculate bash damage (fixed 10 damage on hit).
    /// Legacy: damage(ch, victim, 10, SKILL_BASH)
    /// </summary>
    public static int CalculateDamage()
    {
        return 10;
    }

    /// <summary>
    /// Determine if a bash attack hits the target.
    /// Legacy: percent = number(1, 101); prob = GET_SKILL(ch, SKILL_BASH);
    /// percent > prob = failure
    /// </summary>
    public static bool RollHit(ICombatant attacker)
    {
        int percent = Random.Shared.Next(1, 102); // 1-101 (101 is complete failure)
        int prob = attacker.GetSkill(SkillType.Bash);

        return percent <= prob;
    }
}

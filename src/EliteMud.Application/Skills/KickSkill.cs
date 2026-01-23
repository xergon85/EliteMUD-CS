using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Kick skill - unarmed attack that can initiate or continue combat.
/// 
/// Legacy: skill_kick() in fight.c
/// - Damage: player.Level / 2 (minimum 1)
/// - Success roll: ((10 - victim_ac) * 2) + random(1, 102) vs skill proficiency
/// - Can initiate combat or attack current fighting target
/// - Improves on successful hit
/// 
/// Metadata loaded from content/skills/skills.json
/// </summary>
public sealed class KickSkill : ISkillHandler
{
    private readonly SkillMetadata _metadata;

    public KickSkill(SkillMetadataRegistry registry)
    {
        _metadata = registry.GetBySkillType(SkillType.Kick)
            ?? throw new InvalidOperationException("Kick skill metadata not found in registry");
    }

    public SkillType SkillType => SkillType.Kick;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public int WaitStateRounds => _metadata.WaitStateRounds;

    public bool CanUse(ICombatant user)
    {
        // Must have at least 1% proficiency
        if (user.GetSkill(SkillType.Kick) == 0)
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
        if (user.GetSkill(SkillType.Kick) == 0)
        {
            return "You don't know how to kick!";
        }

        if (user.Level < MinimumLevel)
        {
            return $"You must be at least level {MinimumLevel} to kick.";
        }

        if (user.Position < Position.Fighting)
        {
            return "You can't kick while sitting down!";
        }

        return "You can't use that skill right now.";
    }

    /// <summary>
    /// Calculate kick damage for a combatant.
    /// Legacy: dam = GET_LEVEL(ch) / 2
    /// </summary>
    public static int CalculateDamage(ICombatant user)
    {
        return Math.Max(1, user.Level / 2);
    }

    /// <summary>
    /// Determine if a kick attack hits the target.
    /// Legacy formula from fight.c:
    /// percent = ((10 - GET_AC(vict) / 10) * 2) + number(1, 101)
    /// success if percent <= GET_SKILL(ch, SKILL_KICK)
    /// Works for any combatant (player, mob, etc.) attacking any target.
    /// </summary>
    public static bool RollHit(ICombatant attacker, ICombatant victim)
    {
        int victimAc = victim.ArmorClass / 10;
        int percent = ((10 - victimAc) * 2) + Random.Shared.Next(1, 102);
        int prob = attacker.GetSkill(SkillType.Kick);

        return percent <= prob;
    }
}

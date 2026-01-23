using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Backstab skill - high damage attack that can only be used on unsuspecting victims.
/// Legacy reference: act.offensive.c:203-256 (do_backstab)
/// 
/// Metadata loaded from content/skills/skills.json
/// </summary>
public sealed class BackstabSkill : ISkillHandler
{
    private readonly SkillMetadata _metadata;

    public BackstabSkill(SkillMetadataRegistry registry)
    {
        _metadata = registry.GetBySkillType(SkillType.Backstab)
            ?? throw new InvalidOperationException("Backstab skill metadata not found in registry");
    }

    public SkillType SkillType => SkillType.Backstab;
    
    public string Name => _metadata.Name;
    
    public string Description => _metadata.Description;
    
    public int MinimumLevel => _metadata.MinimumLevel;
    
    public int WaitStateRounds => _metadata.WaitStateRounds;

    public bool CanUse(ICombatant user)
    {
        // Must have skill proficiency
        if (user.GetSkill(SkillType) == 0)
            return false;

        // Must be in standing or fighting position (not sitting/resting/sleeping)
        if (user.Position < Position.Fighting)
            return false;

        return true;
    }

    public string GetCannotUseMessage(ICombatant user)
    {
        if (user.GetSkill(SkillType) == 0)
            return "You don't know how to backstab.";

        if (user.Position < Position.Fighting)
            return "You can't backstab from this position.";

        return "You can't backstab right now.";
    }

    /// <summary>
    /// Calculate backstab damage multiplier based on attacker level.
    /// Legacy formula: MIN(level / 10 + 1, 5)
    /// Reference: fight.c:1520-1521
    /// </summary>
    public static int CalculateDamageMultiplier(ICombatant attacker)
    {
        // Level 1-9: 1x multiplier
        // Level 10-19: 2x multiplier
        // Level 20-29: 3x multiplier
        // Level 30-39: 4x multiplier
        // Level 40+: 5x multiplier (capped)
        return Math.Min(attacker.Level / 10 + 1, 5);
    }

    /// <summary>
    /// Roll to see if backstab hits.
    /// Legacy formula: random(1, 101) vs skill_percent
    /// 101 is automatic failure.
    /// Reference: act.offensive.c:244-251
    /// </summary>
    public static bool RollHit(ICombatant attacker)
    {
        var roll = Random.Shared.Next(1, 102); // 1-101
        var skillPercent = attacker.GetSkill(SkillType.Backstab);

        // If victim is asleep, auto-hit
        // (checked in executor - if awake, use normal roll)
        return roll <= skillPercent;
    }
}

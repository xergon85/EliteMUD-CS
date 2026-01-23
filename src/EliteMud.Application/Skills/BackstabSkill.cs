using EliteMud.Game;
using EliteMud.Scripting;

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
    private readonly FormulaEvaluator _formulaEvaluator;

    public BackstabSkill(SkillMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySkillType(SkillType.Backstab)
            ?? throw new InvalidOperationException("Backstab skill metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
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
    /// Calculate backstab damage multiplier using Lua formula from skills.json.
    /// Formula: "return math.min(math.floor(level / 10) + 1, 5)"
    /// Legacy formula: MIN(level / 10 + 1, 5)
    /// Reference: fight.c:1520-1521
    /// </summary>
    public int CalculateDamageMultiplier(ICombatant attacker)
    {
        var multiplierFormula = _metadata.Mechanics?.DamageMultiplierFormula;
        if (string.IsNullOrEmpty(multiplierFormula))
        {
            // Fallback to legacy hardcoded logic
            return Math.Min(attacker.Level / 10 + 1, 5);
        }

        var context = new
        {
            level = attacker.Level
        };

        return _formulaEvaluator.EvaluateInt(multiplierFormula, context);
    }

    /// <summary>
    /// Roll to see if backstab hits using Lua formula from skills.json.
    /// Formula: "return random(1,101) <= skillPercent"
    /// Legacy formula: random(1, 101) vs skill_percent
    /// 101 is automatic failure.
    /// Reference: act.offensive.c:244-251
    /// </summary>
    public bool RollHit(ICombatant attacker)
    {
        var hitFormula = _metadata.Mechanics?.HitFormula;
        if (string.IsNullOrEmpty(hitFormula))
        {
            // Fallback to legacy hardcoded logic
            var roll = Random.Shared.Next(1, 102);
            return roll <= attacker.GetSkill(SkillType.Backstab);
        }

        var context = new
        {
            skillPercent = attacker.GetSkill(SkillType.Backstab)
        };

        // If victim is asleep, auto-hit
        // (checked in executor - if awake, use normal roll)
        return _formulaEvaluator.EvaluateBool(hitFormula, context);
    }
}

using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Skills;

/// <summary>
/// Kick skill - unarmed attack that can initiate or continue combat.
/// 
/// Legacy: skill_kick() in fight.c
/// - Damage: evaluated from Lua formula in skills.json
/// - Hit chance: evaluated from Lua formula in skills.json
/// - Can initiate combat or attack current fighting target
/// - Improves on successful hit
/// 
/// Metadata and formulas loaded from content/skills/skills.json
/// </summary>
public sealed class KickSkill : ISkillHandler
{
    private readonly SkillMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public KickSkill(SkillMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySkillType(SkillType.Kick)
            ?? throw new InvalidOperationException("Kick skill metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
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
    /// Calculate kick damage for a combatant using Lua formula from skills.json.
    /// Formula: "return math.max(1, level / 2)"
    /// </summary>
    public int CalculateDamage(ICombatant user)
    {
        if (_metadata.Mechanics?.DamageFormula == null)
        {
            throw new InvalidOperationException("Kick damage formula not found in metadata");
        }

        return _formulaEvaluator.EvaluateInt(
            _metadata.Mechanics.DamageFormula,
            new { level = user.Level }
        );
    }

    /// <summary>
    /// Determine if a kick attack hits the target using Lua formula from skills.json.
    /// Formula: "return ((10 - victimAC/10) * 2) + random(1,101) <= skillPercent"
    /// Works for any combatant (player, mob, etc.) attacking any target.
    /// </summary>
    public bool RollHit(ICombatant attacker, ICombatant victim)
    {
        if (_metadata.Mechanics?.HitFormula == null)
        {
            throw new InvalidOperationException("Kick hit formula not found in metadata");
        }

        return _formulaEvaluator.EvaluateBool(
            _metadata.Mechanics.HitFormula,
            new
            {
                victimAC = victim.ArmorClass,
                skillPercent = attacker.GetSkill(SkillType.Kick)
            }
        );
    }
}

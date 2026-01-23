using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Skills;

/// <summary>
/// Dodge skill - passive defensive skill that reduces incoming damage.
/// 
/// Legacy: fight.c:1543-1551
/// - Activation: random(1, 250) + damage < skill%
/// - Effect: Reduce damage by (level * 2)
/// - Improves on successful dodge
/// 
/// Metadata loaded from content/skills/skills.json
/// </summary>
public sealed class DodgeSkill : IPassiveSkillHandler
{
    private readonly SkillMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public DodgeSkill(SkillMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySkillType(SkillType.Dodge)
            ?? throw new InvalidOperationException("Dodge skill metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SkillType SkillType => SkillType.Dodge;

    public string Name => _metadata.Name;

    public string Description => _metadata.Description;

    public int MinimumLevel => _metadata.MinimumLevel;

    public bool CanActivate(ICombatant user)
    {
        // Must have the skill
        if (!user.HasSkill(SkillType.Dodge))
        {
            return false;
        }

        // Must be at minimum level
        if (user.Level < MinimumLevel)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempt to dodge incoming damage using Lua formulas from skills.json.
    /// Activation formula: "return (random(1,250) + damage) < skillPercent"
    /// Effect formula: "return math.max(0, damage - (level * 2))"
    /// Legacy formula: (random(1, 250) + damage) &lt; GET_SKILL(victim, SKILL_DODGE)
    /// If successful, reduce damage by (level * 2).
    /// </summary>
    /// <param name="user">The combatant attempting to dodge</param>
    /// <param name="inputValue">Incoming damage</param>
    /// <returns>Result with potentially reduced damage</returns>
    public PassiveSkillResult TryActivate(ICombatant user, int inputValue)
    {
        // Check if can activate
        if (!CanActivate(user))
        {
            return new PassiveSkillResult(false, inputValue, null);
        }

        int damage = inputValue;
        int dodgeSkillLevel = user.GetSkill(SkillType.Dodge);

        // Check if dodge activates using Lua formula
        var activationFormula = _metadata.Mechanics?.ActivationFormula;
        bool activated;

        if (string.IsNullOrEmpty(activationFormula))
        {
            // Fallback to legacy hardcoded logic
            int check = Random.Shared.Next(1, 251) + damage;
            activated = check < dodgeSkillLevel;
        }
        else
        {
            var context = new
            {
                damage = damage,
                skillPercent = dodgeSkillLevel
            };
            activated = _formulaEvaluator.EvaluateBool(activationFormula, context);
        }

        if (activated)
        {
            // Calculate reduced damage using Lua formula
            var effectFormula = _metadata.Mechanics?.EffectFormula;
            int modifiedDamage;

            if (string.IsNullOrEmpty(effectFormula))
            {
                // Fallback to legacy hardcoded logic
                int reduction = user.Level * 2;
                modifiedDamage = Math.Max(0, damage - reduction);
            }
            else
            {
                var context = new
                {
                    damage = damage,
                    level = user.Level
                };
                modifiedDamage = _formulaEvaluator.EvaluateInt(effectFormula, context);
            }

            return new PassiveSkillResult(
                Activated: true,
                ModifiedValue: modifiedDamage,
                Message: "You dodge the attack!");
        }

        // Dodge failed
        return new PassiveSkillResult(false, damage, null);
    }
}

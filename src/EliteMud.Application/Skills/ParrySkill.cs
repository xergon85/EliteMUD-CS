using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Application.Skills;

/// <summary>
/// Parry skill - passive defensive skill that blocks incoming damage with a shield.
/// 
/// Legacy reference: fight.c:1523-1535
/// - Requirements: Must have shield equipped
/// - Activation formula: (random(1, 300) + damage) < skill%
/// - Effect: Reduce damage by (level) - so victim takes (damage - level)
/// - Improves on successful parry
/// - TODO: Shield/weapon durability damage (not yet implemented)
/// 
/// Metadata loaded from content/skills/skills.json
/// </summary>
public sealed class ParrySkill : IPassiveSkillHandler
{
    private readonly SkillMetadata _metadata;
    private readonly FormulaEvaluator _formulaEvaluator;

    public ParrySkill(SkillMetadataRegistry registry, FormulaEvaluator formulaEvaluator)
    {
        _metadata = registry.GetBySkillType(SkillType.Parry)
            ?? throw new InvalidOperationException("Parry skill metadata not found in registry");
        _formulaEvaluator = formulaEvaluator;
    }

    public SkillType SkillType => SkillType.Parry;
    
    public string Name => _metadata.Name;
    
    public string Description => _metadata.Description;
    
    public int MinimumLevel => _metadata.MinimumLevel;
    
    public bool CanActivate(ICombatant user)
    {
        // Must have the skill
        if (!user.HasSkill(SkillType.Parry))
        {
            return false;
        }
        
        // Must be at minimum level
        if (user.Level < MinimumLevel)
        {
            return false;
        }
        
        // TODO: Must have shield equipped
        // Legacy: victim->equipment[WEAR_SHIELD]
        // For now, allow parry without shield check (will add when equipment system is implemented)
        
        return true;
    }
    
    /// <summary>
    /// Attempt to parry incoming damage using Lua formulas from skills.json.
    /// Activation formula: "return (random(1,300) + damage) < skillPercent"
    /// Effect formula: "return math.max(0, damage - level)"
    /// Legacy formula: (random(1, 300) + damage) < GET_SKILL(victim, SKILL_PARRY)
    /// If successful, reduce damage by user level.
    /// </summary>
    /// <param name="user">The combatant attempting to parry</param>
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
        int parrySkillLevel = user.GetSkill(SkillType.Parry);
        
        // Check if parry activates using Lua formula
        var activationFormula = _metadata.Mechanics?.ActivationFormula;
        bool activated;

        if (string.IsNullOrEmpty(activationFormula))
        {
            // Fallback to legacy hardcoded logic
            int check = Random.Shared.Next(1, 301) + damage;
            activated = check < parrySkillLevel;
        }
        else
        {
            var context = new
            {
                damage = damage,
                skillPercent = parrySkillLevel
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
                int reduction = user.Level;
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
                Message: "You parry the attack with your shield!");
        }
        
        // Parry failed
        return new PassiveSkillResult(false, damage, null);
    }
}

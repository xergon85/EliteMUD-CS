namespace EliteMud.Game;

/// <summary>
/// Parry skill - passive defensive skill that blocks incoming damage with a shield.
/// 
/// Legacy reference: fight.c:1523-1535
/// - Requirements: Must have shield equipped
/// - Activation formula: (random(1, 300) + damage) < skill%
/// - Effect: Reduce damage by (level) - so victim takes (damage - level)
/// - Improves on successful parry
/// - TODO: Shield/weapon durability damage (not yet implemented)
/// </summary>
public sealed class ParrySkill : IPassiveSkillHandler
{
    public SkillType SkillType => SkillType.Parry;
    
    public string Name => "Parry";
    
    public string Description => "Passively block incoming attacks with your shield, reducing damage taken";
    
    public int MinimumLevel => 1;
    
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
    /// Attempt to parry incoming damage.
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
        
        // Legacy: if ((number(1, 300) + dam) < GET_SKILL(victim, SKILL_PARRY))
        // Note: Parry uses 1-300 range (harder to activate than dodge's 1-250)
        int check = Random.Shared.Next(1, 301) + damage;
        
        if (check < parrySkillLevel)
        {
            // Parry successful - reduce damage by user level
            // Legacy: damage(ch, victim, dam - GET_LEVEL(victim), SKILL_PARRY)
            int reduction = user.Level;
            int modifiedDamage = Math.Max(0, damage - reduction);
            
            return new PassiveSkillResult(
                Activated: true,
                ModifiedValue: modifiedDamage,
                Message: "You parry the attack with your shield!");
        }
        
        // Parry failed
        return new PassiveSkillResult(false, damage, null);
    }
}

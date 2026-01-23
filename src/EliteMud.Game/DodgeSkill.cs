namespace EliteMud.Game;

/// <summary>
/// Dodge skill - passive defensive skill that reduces incoming damage.
/// 
/// Legacy: fight.c:1543-1551
/// - Activation: random(1, 250) + damage &lt; skill%
/// - Effect: Reduce damage by (level * 2)
/// - Improves on successful dodge
/// </summary>
public sealed class DodgeSkill : IPassiveSkillHandler
{
    public SkillType SkillType => SkillType.Dodge;

    public string Name => "Dodge";

    public string Description => "Passively avoid incoming attacks, reducing damage taken";

    public int MinimumLevel => 1;

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
    /// Attempt to dodge incoming damage.
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

        // Legacy: if ((number(1, 250) + damage) < GET_SKILL(victim, SKILL_DODGE))
        int check = Random.Shared.Next(1, 251) + damage;

        if (check < dodgeSkillLevel)
        {
            // Dodge successful - reduce damage by 2x user level
            // Legacy: dam -= (GET_LEVEL(victim) * 2)
            int reduction = user.Level * 2;
            int modifiedDamage = Math.Max(0, damage - reduction);

            return new PassiveSkillResult(
                Activated: true,
                ModifiedValue: modifiedDamage,
                Message: "You dodge the attack!");
        }

        // Dodge failed
        return new PassiveSkillResult(false, damage, null);
    }
}

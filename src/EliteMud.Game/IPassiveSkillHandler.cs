namespace EliteMud.Game;

/// <summary>
/// Result of a passive skill activation.
/// </summary>
public sealed record PassiveSkillResult(
    bool Activated,
    int ModifiedValue,
    string? Message);

/// <summary>
/// Contract for passive skill implementations.
/// Passive skills trigger automatically during combat or other actions (dodge, parry, riposte, etc.).
/// 
/// This interface defines domain logic for passive skills, independent of infrastructure concerns.
/// Passive skill handlers should be stateless and focus on skill mechanics.
/// 
/// NOTE: Unlike ISkillHandler (active skills), passive skills are triggered by the system, not by player commands.
/// Active skills (kick, bash, etc.) have CommandHandlers in the Server layer.
/// Passive skills (dodge, parry, etc.) are invoked directly from domain code (CombatCalculator, etc.).
/// 
/// Future: When we refactor to a SkillRegistry, both ISkillHandler and IPassiveSkillHandler
/// will be registered together, allowing unified skill discovery and metadata access.
/// </summary>
public interface IPassiveSkillHandler
{
    /// <summary>
    /// The skill type this handler implements.
    /// </summary>
    SkillType SkillType { get; }
    
    /// <summary>
    /// Display name of the skill (e.g., "Dodge", "Parry").
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description shown in skill lists and help text.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Minimum level required to use this skill.
    /// Returns 0 if no level requirement.
    /// </summary>
    int MinimumLevel { get; }
    
    /// <summary>
    /// Check if the combatant can use this passive skill.
    /// Validates: skill proficiency > 0, minimum level, etc.
    /// </summary>
    /// <param name="user">The combatant that would use the skill</param>
    /// <returns>True if the skill can activate, false otherwise</returns>
    bool CanActivate(ICombatant user);
    
    /// <summary>
    /// Attempt to activate the passive skill.
    /// Called automatically during combat when appropriate.
    /// </summary>
    /// <param name="user">The combatant using the skill</param>
    /// <param name="inputValue">The value to potentially modify (e.g., incoming damage)</param>
    /// <returns>Result indicating if skill activated and the modified value</returns>
    PassiveSkillResult TryActivate(ICombatant user, int inputValue);
}

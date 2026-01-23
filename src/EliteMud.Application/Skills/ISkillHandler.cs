using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Result of a skill execution.
/// </summary>
public sealed record SkillExecutionResult(
    bool Success,
    string? Message,
    bool ImprovedSkill = false);

/// <summary>
/// Contract for active skill implementations.
/// Active skills are player-initiated actions like kick, bash, backstab, etc.
/// 
/// This interface defines domain logic for skills, independent of infrastructure concerns.
/// Skill handlers should be stateless and focus on skill mechanics.
/// </summary>
public interface ISkillHandler
{
    /// <summary>
    /// The skill type this handler implements.
    /// </summary>
    SkillType SkillType { get; }
    
    /// <summary>
    /// Display name of the skill (e.g., "Kick", "Bash").
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
    /// Number of combat rounds the player must wait after using this skill.
    /// Legacy: WAIT_STATE value (1 round = 2 seconds)
    /// Returns 0 for no cooldown.
    /// </summary>
    int WaitStateRounds { get; }
    
    /// <summary>
    /// Check if the combatant can use this skill right now.
    /// Validates: skill proficiency > 0, position, minimum level, etc.
    /// Note: Does NOT check wait state - that's handled by the command layer.
    /// </summary>
    /// <param name="user">The combatant (player or mob) attempting to use the skill</param>
    /// <returns>True if the skill can be used, false otherwise</returns>
    bool CanUse(ICombatant user);
    
    /// <summary>
    /// Get a failure message explaining why the skill cannot be used.
    /// Only called if CanUse() returns false.
    /// </summary>
    /// <param name="user">The combatant (player or mob) attempting to use the skill</param>
    /// <returns>User-friendly error message</returns>
    string GetCannotUseMessage(ICombatant user);
}

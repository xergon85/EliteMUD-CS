using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// How a skill finds/requires targets. Determines command parsing behavior.
/// </summary>
public enum TargetingMode
{
    /// <summary>
    /// No target required (e.g., 'skills' command to list skills, 'meditate')
    /// </summary>
    None,
    
    /// <summary>
    /// Auto-target current fighting opponent if no target specified.
    /// Can also specify target by name to start combat or kick current target.
    /// Used by: kick, bash, circle, etc.
    /// </summary>
    CurrentFightTarget,
    
    /// <summary>
    /// Must specify target in room (cannot auto-target fighting opponent).
    /// Used by: backstab, steal, etc.
    /// </summary>
    RequiredInRoom,
    
    /// <summary>
    /// Requires a direction argument (north, south, east, west, up, down).
    /// Used by: scout, track, etc.
    /// </summary>
    Direction,
    
    /// <summary>
    /// Targets the user themselves.
    /// Used by: self-buff skills, meditation, etc.
    /// </summary>
    Self
}

/// <summary>
/// Who sees a skill message.
/// Maps to ActTarget flags for consistency with existing message system.
/// </summary>
public enum SkillMessageTarget
{
    /// <summary>
    /// Send to the actor performing the skill (ActTarget.ToChar)
    /// </summary>
    Actor,
    
    /// <summary>
    /// Send to the victim of the skill (ActTarget.ToVict)
    /// </summary>
    Victim,
    
    /// <summary>
    /// Send to everyone in the room (ActTarget.ToRoom)
    /// </summary>
    Room,
    
    /// <summary>
    /// Send to everyone in room except actor and victim (ActTarget.ToNotVict)
    /// </summary>
    Others
}

/// <summary>
/// A message to send as result of skill execution.
/// Uses act() template format: $n (actor), $N (victim), $e/$m/$s (pronouns)
/// </summary>
public sealed record SkillMessage(
    SkillMessageTarget Target,
    string Template,
    object? Victim = null);

/// <summary>
/// Result of skill execution.
/// Contains all information needed for the Server layer to handle I/O.
/// </summary>
public sealed record SkillResult(
    bool Success,
    SkillMessage[] Messages)
{
    /// <summary>
    /// Create a failure result with a single message to the actor.
    /// </summary>
    public static SkillResult Failed(string message)
    {
        return new SkillResult(
            Success: false,
            Messages: [new SkillMessage(SkillMessageTarget.Actor, message)]);
    }
    
    /// <summary>
    /// Create a failure result with a custom message.
    /// </summary>
    public static SkillResult Failed(SkillMessage message)
    {
        return new SkillResult(
            Success: false,
            Messages: [message]);
    }
    
    /// <summary>
    /// Create a success result with messages.
    /// </summary>
    public static SkillResult Succeeded(params SkillMessage[] messages)
    {
        return new SkillResult(
            Success: true,
            Messages: messages);
    }
}

/// <summary>
/// Context passed to skill executors containing all execution parameters.
/// </summary>
public sealed record SkillContext(
    PlayerState Actor,
    int ActorConnectionId,
    ICombatant? Victim,
    int? VictimConnectionId,
    string? Argument);

/// <summary>
/// Base interface for all skill executors.
/// Skill executors contain ALL business logic for skill execution.
/// The Server layer uses a generic SkillCommandHandler that routes to executors.
/// 
/// DESIGN PHILOSOPHY:
/// - Application layer (executor): Business logic, skill execution, returns SkillResult
/// - Server layer (generic handler): Target resolution, message formatting, I/O
/// - Game layer (formulas): Pure calculations (hit rolls, damage, etc.)
/// 
/// This makes adding new skills trivial - just implement ISkillExecutor and it
/// automatically becomes a command via auto-registration.
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// The skill type this executor implements.
    /// </summary>
    SkillType SkillType { get; }
    
    /// <summary>
    /// How this skill selects targets.
    /// Determines how the generic handler parses command arguments.
    /// </summary>
    TargetingMode Targeting { get; }
    
    /// <summary>
    /// Execute the skill with the given context.
    /// Contains ALL business logic - combat initiation, damage, death, skill improvement, etc.
    /// </summary>
    /// <param name="context">Execution context with actor, victim, arguments</param>
    /// <returns>Result with messages to send to players</returns>
    SkillResult Execute(SkillContext context);
}

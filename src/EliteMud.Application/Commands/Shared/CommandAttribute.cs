namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Defines a command that players can execute.
/// Annotate command handlers/executors with this attribute to enable auto-registration.
/// 
/// Example:
///   [Command("kick", Aliases = new[] { "k" })]
///   public class KickSkillExecutor : ISkillExecutor { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// Primary command name (e.g., "kick", "look", "inventory")
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Alternative names for this command (e.g., "k" for "kick", "i" for "inventory")
    /// </summary>
    public string[] Aliases { get; init; } = [];

    /// <summary>
    /// Admin and test commands (zreset, goto, setlevel, ...). Players who aren't admins get the unknown-command
    /// reply, as if the command didn't exist (legacy "Huh?!" for commands above your level).
    /// </summary>
    public bool Admin { get; init; }
    
    public CommandAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name cannot be empty", nameof(name));
        
        Name = name.ToLowerInvariant();
    }
}

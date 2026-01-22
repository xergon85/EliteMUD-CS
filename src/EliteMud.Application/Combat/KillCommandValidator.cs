using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// Validates preconditions for the kill command.
/// Keeps validation logic separate from command execution.
/// </summary>
public static class KillCommandValidator
{
    /// <summary>
    /// Validate that a player can initiate combat.
    /// </summary>
    /// <param name="player">The attacking player</param>
    /// <param name="targetName">The target name (can be null/empty)</param>
    /// <returns>Validation result with error message if invalid</returns>
    public static ValidationResult Validate(PlayerState player, string? targetName)
    {
        // Check if target name is provided
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return ValidationResult.Fail("Kill whom?");
        }

        // Check if already fighting
        if (player.FightingConnectionId != null)
        {
            return ValidationResult.Fail("You're already fighting!");
        }

        return ValidationResult.Success();
    }
}

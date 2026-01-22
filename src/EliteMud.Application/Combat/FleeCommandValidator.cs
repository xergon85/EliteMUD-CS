using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// Validates preconditions for the flee command.
/// Keeps validation logic separate from command execution.
/// Legacy reference: act.offensive.c:388
/// </summary>
public static class FleeCommandValidator
{
    /// <summary>
    /// Validate that a player can attempt to flee.
    /// </summary>
    /// <param name="player">The player attempting to flee</param>
    /// <returns>Validation result with error message if invalid</returns>
    public static ValidationResult Validate(PlayerState player)
    {
        // Check if in bad position (must be at least Fighting position)
        if (player.Position < Position.Fighting)
        {
            return ValidationResult.Fail("You are in pretty bad shape, unable to flee!");
        }

        return ValidationResult.Success();
    }
}

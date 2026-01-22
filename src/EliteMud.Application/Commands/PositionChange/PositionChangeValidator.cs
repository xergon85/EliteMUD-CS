using EliteMud.Application.Commands.Shared;
using EliteMud.Game;

namespace EliteMud.Application.Commands.PositionChange;

/// <summary>
/// Validates preconditions for position change commands.
/// Centralizes common validation logic for stand, sit, rest, sleep, and wake commands.
/// </summary>
public static class PositionChangeValidator
{
    /// <summary>
    /// Validate that a player can change to a new position.
    /// </summary>
    /// <param name="player">The player attempting to change position</param>
    /// <param name="targetPosition">The desired position</param>
    /// <param name="positionName">Human-readable name of the position (e.g., "standing", "sitting")</param>
    /// <returns>Validation result with error message if invalid</returns>
    public static ValidationResult Validate(
        PlayerState player, 
        Game.Position targetPosition,
        string positionName)
    {
        // Check if already in target position
        if (player.Position == targetPosition)
        {
            return ValidationResult.Fail($"You are already {positionName}.");
        }

        // Check if fighting (can't change position while in combat)
        if (player.FightingConnectionId != null)
        {
            return ValidationResult.Fail($"You can't {GetActionVerb(targetPosition)} while fighting!");
        }

        // Check if position is too low (stunned, incapacitated, mortally wounded, or dead)
        if (player.Position < Game.Position.Stunned)
        {
            return ValidationResult.Fail($"You can't {GetActionVerb(targetPosition)} in your current state.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Special validation for wake command which has different logic.
    /// </summary>
    public static ValidationResult ValidateWake(PlayerState player)
    {
        // Already awake (sitting or above)
        if (player.Position >= Game.Position.Sitting)
        {
            return ValidationResult.Fail("You are already awake.");
        }

        // Check if position is too low
        if (player.Position < Game.Position.Stunned)
        {
            return ValidationResult.Fail("You can't wake up in your current state.");
        }

        return ValidationResult.Success();
    }

    private static string GetActionVerb(Game.Position targetPosition)
    {
        return targetPosition switch
        {
            Game.Position.Standing => "stand",
            Game.Position.Sitting => "sit",
            Game.Position.Resting => "rest",
            Game.Position.Sleeping => "sleep",
            _ => "change position"
        };
    }
}

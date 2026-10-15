using System.Linq;
using EliteMud.Application.Progression;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Flee;

/// <summary>
/// Handler for flee attempts (both manual flee command and wimpy auto-flee).
/// Centralizes flee logic to avoid duplication between FleeCommandHandler and GameTickService.
/// Legacy reference: act.offensive.c:379-478
/// </summary>
public sealed class FleeHandler
{
    private readonly IWorldState _worldState;
    private readonly PlayerProgression _progression;

    public FleeHandler(IWorldState worldState, PlayerProgression progression)
    {
        _worldState = worldState;
        _progression = progression;
    }

    /// <summary>
    /// Attempts to flee in a random direction.
    /// Returns FleeResult indicating success/failure and any state changes.
    /// </summary>
    public FleeResult AttemptFlee(
        PlayerState player,
        int currentRoomId,
        Func<IEnumerable<MobInstance>> getMobsInRoom)
    {
        // Try to flee in 6 random directions (legacy: act.offensive.c:431)
        var random = new Random();
        var directions = new[] {
            Direction.North, Direction.East, Direction.South,
            Direction.West, Direction.Up, Direction.Down
        };

        for (int i = 0; i < 6; i++)
        {
            var attemptDirection = directions[random.Next(directions.Length)];

            // Check if can move in that direction
            if (!_worldState.World.TryMove(currentRoomId, attemptDirection, out var targetRoomId))
            {
                continue;
            }

            // Closed exits block fleeing, doors or not
            if (_worldState.IsExitClosed(currentRoomId, attemptDirection))
            {
                continue; // Try another direction
            }

            // Calculate experience loss if fighting (legacy: act.offensive.c:425-427)
            int experienceLoss = CalculateExperienceLoss(player, currentRoomId, getMobsInRoom);

            // Move succeeded - return success result
            return new FleeResult(
                Success: true,
                NewRoomId: targetRoomId,
                ExperienceLoss: experienceLoss,
                OldRoomId: currentRoomId
            );
        }

        // No valid exits found (legacy: act.offensive.c:421)
        return new FleeResult(
            Success: false,
            NewRoomId: currentRoomId,
            ExperienceLoss: 0,
            OldRoomId: currentRoomId
        );
    }

    /// <summary>
    /// Applies the flee result to the player and world state.
    /// Handles movement, combat cleanup, and experience loss.
    /// Returns the experience messages for the player ("You lose N experience points."), to send before the
    /// flee message as legacy does.
    /// </summary>
    public IReadOnlyList<string> ApplyFleeResult(PlayerState player, FleeResult result)
    {
        if (!result.Success)
        {
            return Array.Empty<string>();
        }

        // Leaving the room stops the player's fight
        _worldState.Population.Move(player, result.NewRoomId);

        // Experience loss through the normal rules: never below 0, immortals exempt (legacy gain_exp)
        var loss = result.ExperienceLoss > 0
            ? _progression.GainExperience(player, -result.ExperienceLoss).Messages
            : Array.Empty<string>();

        // Stop all mobs/players that were fighting us in the old room
        var oldRoom = _worldState.GetMobsInRoom(result.OldRoomId)
            .Concat<ICombatant>(_worldState.Population.PlayersIn(result.OldRoomId));
        CombatCalculator.StopEveryoneFighting(player, oldRoom);

        return loss;
    }

    private int CalculateExperienceLoss(
        PlayerState player,
        int currentRoomId,
        Func<IEnumerable<MobInstance>> getMobsInRoom)
    {
        // Experience loss is damage done to the opponent × its level (legacy do_flee); the opponent must still be here
        var opponent = player.Fighting;
        var stillHere = opponent switch
        {
            PlayerState other => _worldState.Population.PlayersIn(currentRoomId).Contains(other),
            MobInstance mob => getMobsInRoom().Contains(mob),
            _ => false
        };

        if (stillHere)
        {
            int damageDone = opponent!.MaxHitPoints - opponent.HitPoints;
            return damageDone * opponent.Level;
        }

        return 0;
    }
}

/// <summary>
/// Result of a flee attempt.
/// </summary>
public sealed record FleeResult(
    bool Success,
    int NewRoomId,
    int ExperienceLoss,
    int OldRoomId
);

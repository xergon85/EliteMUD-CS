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

    public FleeHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    /// <summary>
    /// Attempts to flee in a random direction.
    /// Returns FleeResult indicating success/failure and any state changes.
    /// </summary>
    public FleeResult AttemptFlee(
        PlayerState player,
        int currentRoomId,
        Func<IEnumerable<PlayerState>> getOtherPlayers,
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

            // Calculate experience loss if fighting (legacy: act.offensive.c:425-427)
            int experienceLoss = CalculateExperienceLoss(player, currentRoomId, getOtherPlayers, getMobsInRoom);

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
    /// </summary>
    public void ApplyFleeResult(
        PlayerState player,
        FleeResult result,
        Func<IEnumerable<PlayerState>> getOtherPlayers,
        int playerConnectionId)
    {
        if (!result.Success)
        {
            return;
        }

        // Move to new room
        player.RoomId = result.NewRoomId;

        // Stop fighting
        CombatCalculator.StopFighting(player);

        // Apply experience loss
        if (result.ExperienceLoss > 0)
        {
            player.Experience -= result.ExperienceLoss;
            if (player.Experience < 0) player.Experience = 0;
        }

        // Stop all mobs/players that were fighting us in the old room
        var mobsInOldRoom = _worldState.GetMobsInRoom(result.OldRoomId);
        foreach (var mob in mobsInOldRoom)
        {
            if (mob.FightingConnectionId == playerConnectionId)
            {
                mob.FightingConnectionId = null;
                mob.Position = Position.Standing;
            }
        }

        var playersInOldRoom = getOtherPlayers()
            .Where(p => p.RoomId == result.OldRoomId && p.FightingConnectionId == playerConnectionId);
        foreach (var otherPlayer in playersInOldRoom)
        {
            CombatCalculator.StopFighting(otherPlayer);
        }
    }

    private int CalculateExperienceLoss(
        PlayerState player,
        int currentRoomId,
        Func<IEnumerable<PlayerState>> getOtherPlayers,
        Func<IEnumerable<MobInstance>> getMobsInRoom)
    {
        if (player.FightingConnectionId == null)
        {
            return 0;
        }

        var victimId = player.FightingConnectionId.Value;
        if (victimId > 0)
        {
            // Fighting a player
            var victim = getOtherPlayers().FirstOrDefault(p => p.Id == victimId);
            if (victim != null)
            {
                int damageDone = victim.MaxHitPoints - victim.HitPoints;
                return damageDone * victim.Level;
            }
        }
        else
        {
            // Fighting a mob
            var mobInstanceId = -victimId;
            var mob = getMobsInRoom().FirstOrDefault(m => m.InstanceId == mobInstanceId);
            if (mob != null)
            {
                int mobMaxHp = Math.Max(mob.HitPoints, mob.Definition.Level * 10);
                int damageDone = mobMaxHp - mob.HitPoints;
                return damageDone * mob.Definition.Level;
            }
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

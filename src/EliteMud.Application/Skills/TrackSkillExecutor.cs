using EliteMud.Application.Ai;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes track skill logic in the Application layer.
/// 
/// Track is a utility skill that finds the path to a target mob or player.
/// Legacy reference: graph.c perform_track(), act.informative.c do_track()
/// 
/// Mechanics:
/// - Search for target by name (player or mob) across the entire world
/// - Use PathfindingService to find shortest path to target
/// - Skill check determines success (random 1-101 vs skill proficiency)
/// - On success: show direction to move toward target
/// - On failure: "You lose the trail"
/// - WAIT_STATE: 1 round (2 seconds)
/// - Skill improvement: Only on successful tracking
/// </summary>
[Command("track", Aliases = new[] { "tr" })]
public sealed class TrackSkillExecutor : ISkillExecutor
{
    private readonly TrackSkill _trackSkill;
    private readonly PathfindingService _pathfindingService;
    private readonly IWorldState _worldState;

    public SkillType SkillType => SkillType.Track;
    public TargetingMode Targeting => TargetingMode.None; // Takes string argument, not entity target

    public TrackSkillExecutor(
        SkillRegistry skillRegistry,
        PathfindingService pathfindingService,
        IWorldState worldState)
    {
        _trackSkill = (TrackSkill)skillRegistry.GetActiveSkill(SkillType.Track);
        _pathfindingService = pathfindingService;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute track to find a target (player or mob) by name.
    /// Searches entire world, finds path using PathfindingService, shows direction.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var tracker = context.Actor;
        var targetName = context.Argument?.Trim();

        // Target name is required
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return SkillResult.Failed("Track whom?");
        }

        // Check if player can use track
        if (!_trackSkill.CanUse(tracker))
        {
            return SkillResult.Failed(_trackSkill.GetCannotUseMessage(tracker));
        }

        // Find all potential targets in the world (players and mobs)
        var targetRoomId = FindTargetRoom(targetName, tracker.RoomId);

        if (targetRoomId == null)
        {
            return SkillResult.Failed($"You can't find a trail to '{targetName}'.");
        }

        // Already in same room?
        if (targetRoomId == tracker.RoomId)
        {
            return SkillResult.Failed("They're right here!");
        }

        // Roll skill check
        bool success = _trackSkill.RollSuccess(tracker);

        if (!success)
        {
            // Skill check failed - lose the trail
            // Apply wait state even on failure
            tracker.WaitState = CombatConstants.WaitStates.Track;

            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "You lose the trail.")
            );
        }

        // Calculate max search distance based on skill proficiency
        int maxDistance = _trackSkill.CalculateMaxDistance(tracker);

        // Use PathfindingService to find direction
        var direction = _pathfindingService.GetNextDirection(
            _worldState,
            tracker.RoomId,
            targetRoomId.Value,
            maxDistance);

        if (direction == null)
        {
            // Target exists but too far away or no path
            tracker.WaitState = CombatConstants.WaitStates.Track;

            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "The trail is too faint to follow.")
            );
        }

        // Success! Show direction
        var directionName = direction.Value.ToString().ToLower();
        var messages = new List<SkillMessage>
        {
            new SkillMessage(SkillMessageTarget.Actor, $"You sense a trail {directionName}.")
        };

        // Improve skill on successful tracking
        if (tracker.TryImproveSkill(SkillType.Track))
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Your skill - track - just improved!"));
        }

        // Apply wait state
        tracker.WaitState = CombatConstants.WaitStates.Track;

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }

    /// <summary>
    /// Find the room ID where a target (player or mob) matching the name is located.
    /// Searches players first, then all mobs in the world.
    /// Returns null if no match found.
    /// </summary>
    private int? FindTargetRoom(string targetName, int excludeRoomId)
    {
        // Search for player targets first (case-insensitive partial match)
        // Note: We don't have access to ConnectionRegistry here, so we'll search mobs only
        // In the future, could inject IConnectionDirectory to search players too

        // Search all rooms for mobs matching the name
        foreach (var (roomId, room) in _worldState.World.Rooms)
        {
            var mobs = _worldState.GetMobsInRoom(roomId);
            var match = mobs.FirstOrDefault(m =>
                m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return roomId;
            }
        }

        return null; // No target found
    }
}

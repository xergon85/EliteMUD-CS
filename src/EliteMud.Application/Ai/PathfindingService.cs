using System.Linq;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Ai;

/// <summary>
/// Pathfinding service for finding shortest paths between rooms.
/// Uses BFS (Breadth-First Search) for unweighted room graphs.
/// Legacy: graph.c perform_track()
/// </summary>
public class PathfindingService
{
    /// <summary>
    /// Find the shortest path from startRoomId to targetRoomId.
    /// Returns a queue of room IDs representing the path (excluding start room).
    /// Returns null if no path exists within maxDistance.
    /// 
    /// Legacy: perform_track(ch, target_room, max_distance)
    /// </summary>
    /// <param name="worldState">Current world state containing room graph</param>
    /// <param name="startRoomId">Starting room ID</param>
    /// <param name="targetRoomId">Destination room ID</param>
    /// <param name="maxDistance">Maximum path length to search (default 100)</param>
    /// <param name="respectNoMob">If true, skip rooms with NO_MOB flag (default true)</param>
    /// <param name="stayInZone">If true, only search within starting room's zone (default false)</param>
    /// <returns>Queue of room IDs to follow, or null if no path found</returns>
    public Queue<int>? FindPath(
        IWorldState worldState,
        int startRoomId,
        int targetRoomId,
        int maxDistance = 100,
        bool respectNoMob = true,
        bool stayInZone = false)
    {
        // Validate inputs
        if (!worldState.World.Rooms.TryGetValue(startRoomId, out var startRoom))
        {
            return null; // Start room doesn't exist
        }

        if (!worldState.World.Rooms.TryGetValue(targetRoomId, out var targetRoom))
        {
            return null; // Target room doesn't exist
        }

        if (startRoomId == targetRoomId)
        {
            return new Queue<int>(); // Already at target, empty path
        }

        // BFS queue: each entry is (roomId, pathToRoom)
        var queue = new Queue<(int roomId, List<int> path)>();
        var visited = new HashSet<int>();

        // Start BFS from the starting room
        queue.Enqueue((startRoomId, new List<int>()));
        visited.Add(startRoomId);

        int? startZoneId = startRoom.ZoneId;

        while (queue.Count > 0)
        {
            var (currentRoomId, currentPath) = queue.Dequeue();

            // Check max distance (path length limit)
            if (currentPath.Count >= maxDistance)
            {
                continue; // Don't explore beyond max distance
            }

            // Get current room
            if (!worldState.World.Rooms.TryGetValue(currentRoomId, out var currentRoom))
            {
                continue; // Invalid room, skip
            }

            // Explore all exits from this room
            foreach (var exit in currentRoom.Exits)
            {
                int neighborRoomId = exit.TargetRoomId;

                // Skip if already visited
                if (visited.Contains(neighborRoomId))
                {
                    continue;
                }

                // Check if there is a closed door blocking this exit
                if (exit.IsDoor)
                {
                    var doorState = worldState.GetDoorState(currentRoomId, exit.Direction);
                    if (doorState?.IsClosed == true)
                    {
                        continue; // Door is closed, can't path through it
                    }
                }

                // Get neighbor room
                if (!worldState.World.Rooms.TryGetValue(neighborRoomId, out var neighborRoom))
                {
                    continue; // Invalid exit target
                }

                // Check NO_MOB flag (mobs can't enter)
                if (respectNoMob && neighborRoom.Flags.HasFlag(RoomFlags.NoMob))
                {
                    continue;
                }

                // Check DEATH flag (mobs should avoid death rooms)
                if (neighborRoom.Flags.HasFlag(RoomFlags.Death))
                {
                    continue;
                }

                // Check zone restriction
                if (stayInZone && startZoneId != null && neighborRoom.ZoneId != startZoneId)
                {
                    continue; // Don't leave starting zone
                }

                // Build new path including this neighbor
                var newPath = new List<int>(currentPath) { neighborRoomId };

                // Check if we reached the target
                if (neighborRoomId == targetRoomId)
                {
                    // Found the target! Return the path as a queue
                    return new Queue<int>(newPath);
                }

                // Mark as visited and add to queue
                visited.Add(neighborRoomId);
                queue.Enqueue((neighborRoomId, newPath));
            }
        }

        // No path found within max distance
        return null;
    }

    /// <summary>
    /// Get the next direction to move from currentRoomId towards targetRoomId.
    /// Used by the track skill to show players which way to go.
    /// Returns null if no path exists.
    /// </summary>
    /// <param name="worldState">Current world state</param>
    /// <param name="currentRoomId">Current room ID</param>
    /// <param name="targetRoomId">Target room ID</param>
    /// <param name="maxDistance">Maximum search distance</param>
    /// <returns>Direction to move, or null if no path</returns>
    public Direction? GetNextDirection(
        IWorldState worldState,
        int currentRoomId,
        int targetRoomId,
        int maxDistance = 100)
    {
        // Find path to target
        var path = FindPath(worldState, currentRoomId, targetRoomId, maxDistance);

        if (path == null || path.Count == 0)
        {
            return null; // No path found
        }

        // Get the first room in the path (next step)
        int nextRoomId = path.Peek();

        // Find which exit leads to that room
        if (!worldState.World.Rooms.TryGetValue(currentRoomId, out var currentRoom))
        {
            return null;
        }

        var exit = currentRoom.Exits.FirstOrDefault(e => e.TargetRoomId == nextRoomId);
        return exit?.Direction;
    }
}

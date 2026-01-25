using EliteMud.Application.Ai;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Ai;

/// <summary>
/// Tests for PathfindingService.
/// Validates BFS pathfinding algorithm for room graphs.
/// Legacy reference: graph.c perform_track()
/// </summary>
public class PathfindingServiceTests
{
    // ===== Basic Pathfinding Tests =====

    [Fact]
    public void FindPath_SimpleLinearPath_ReturnsCorrectPath()
    {
        // Arrange: Room 1 -> Room 2 -> Room 3
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 3);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path.Dequeue()); // First step
        Assert.Equal(3, path.Dequeue()); // Second step
    }

    [Fact]
    public void FindPath_SameRoom_ReturnsEmptyPath()
    {
        // Arrange
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 1);

        // Assert
        Assert.NotNull(path);
        Assert.Empty(path); // Already at target
    }

    [Fact]
    public void FindPath_NoPathExists_ReturnsNull()
    {
        // Arrange: Two disconnected rooms
        var worldState = CreateDisconnectedWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 2);

        // Assert
        Assert.Null(path); // No path exists
    }

    [Fact]
    public void FindPath_InvalidStartRoom_ReturnsNull()
    {
        // Arrange
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 999, targetRoomId: 1);

        // Assert
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_InvalidTargetRoom_ReturnsNull()
    {
        // Arrange
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 999);

        // Assert
        Assert.Null(path);
    }

    // ===== Complex Graph Tests =====

    [Fact]
    public void FindPath_MultiplePaths_ReturnsShortestPath()
    {
        // Arrange: Diamond graph
        //     2
        //    / \
        //   1   4
        //    \ /
        //     3
        var worldState = CreateDiamondWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 4);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(2, path.Count); // Should take either 1->2->4 or 1->3->4 (both length 2)
        
        var firstStep = path.Dequeue();
        Assert.True(firstStep == 2 || firstStep == 3); // Either north or south path
        Assert.Equal(4, path.Dequeue()); // Both end at room 4
    }

    [Fact]
    public void FindPath_CyclicGraph_FindsShortestPath()
    {
        // Arrange: Square with diagonal
        //   1 --- 2
        //   |  X  |
        //   3 --- 4
        var worldState = CreateSquareWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 4);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(2, path.Count); // Diagonal: 1->2->4 or 1->3->4
    }

    // ===== NO_MOB Flag Tests =====

    [Fact]
    public void FindPath_RespectNoMob_SkipsNoMobRooms()
    {
        // Arrange: 1 -> 2 (NO_MOB) -> 3
        var worldState = CreateWorldWithNoMobRoom();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 3, respectNoMob: true);

        // Assert
        Assert.Null(path); // Can't reach room 3 without going through NO_MOB room 2
    }

    [Fact]
    public void FindPath_IgnoreNoMob_AllowsNoMobRooms()
    {
        // Arrange: 1 -> 2 (NO_MOB) -> 3
        var worldState = CreateWorldWithNoMobRoom();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 3, respectNoMob: false);

        // Assert - Players can traverse NO_MOB rooms
        Assert.NotNull(path);
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path.Dequeue());
        Assert.Equal(3, path.Dequeue());
    }

    // ===== DEATH Flag Tests =====

    [Fact]
    public void FindPath_SkipsDeathRooms()
    {
        // Arrange: 1 -> 2 (DEATH) -> 3
        var worldState = CreateWorldWithDeathRoom();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 3);

        // Assert
        Assert.Null(path); // Can't reach room 3 without going through DEATH room
    }

    // ===== Zone Restriction Tests =====

    [Fact]
    public void FindPath_StayInZone_RespectsBoundary()
    {
        // Arrange: Room 1 (zone 1) -> Room 2 (zone 2)
        var worldState = CreateMultiZoneWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 2, stayInZone: true);

        // Assert
        Assert.Null(path); // Can't leave zone 1
    }

    [Fact]
    public void FindPath_AllowZoneCrossing_FindsPath()
    {
        // Arrange: Room 1 (zone 1) -> Room 2 (zone 2)
        var worldState = CreateMultiZoneWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 2, stayInZone: false);

        // Assert
        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal(2, path.Dequeue());
    }

    // ===== Max Distance Tests =====

    [Fact]
    public void FindPath_ExceedsMaxDistance_ReturnsNull()
    {
        // Arrange: 1 -> 2 -> 3 -> 4 -> 5
        var worldState = CreateLongLinearWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 5, maxDistance: 2);

        // Assert
        Assert.Null(path); // Path length is 4, exceeds maxDistance of 2
    }

    [Fact]
    public void FindPath_WithinMaxDistance_FindsPath()
    {
        // Arrange: 1 -> 2 -> 3
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var path = service.FindPath(worldState, startRoomId: 1, targetRoomId: 3, maxDistance: 10);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(2, path.Count);
    }

    // ===== GetNextDirection Tests =====

    [Fact]
    public void GetNextDirection_ReturnsCorrectDirection()
    {
        // Arrange: 1 --(north)--> 2
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var direction = service.GetNextDirection(worldState, currentRoomId: 1, targetRoomId: 2);

        // Assert
        Assert.Equal(Direction.North, direction);
    }

    [Fact]
    public void GetNextDirection_NoPath_ReturnsNull()
    {
        // Arrange
        var worldState = CreateDisconnectedWorld();
        var service = new PathfindingService();

        // Act
        var direction = service.GetNextDirection(worldState, currentRoomId: 1, targetRoomId: 2);

        // Assert
        Assert.Null(direction);
    }

    [Fact]
    public void GetNextDirection_SameRoom_ReturnsNull()
    {
        // Arrange
        var worldState = CreateLinearWorld();
        var service = new PathfindingService();

        // Act
        var direction = service.GetNextDirection(worldState, currentRoomId: 1, targetRoomId: 1);

        // Assert
        Assert.Null(direction); // Already at target
    }

    // ===== Helper Methods =====

    private WorldState CreateLinearWorld()
    {
        // Room 1 --(north)--> Room 2 --(north)--> Room 3
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "First room", 
                new List<ExitDefinition> { new(Direction.North, 2) }),
            [2] = new(2, "Room 2", "Second room", 
                new List<ExitDefinition> { new(Direction.South, 1), new(Direction.North, 3) }),
            [3] = new(3, "Room 3", "Third room", 
                new List<ExitDefinition> { new(Direction.South, 2) })
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateLongLinearWorld()
    {
        // 1 -> 2 -> 3 -> 4 -> 5
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "First room", 
                new List<ExitDefinition> { new(Direction.North, 2) }),
            [2] = new(2, "Room 2", "Second room", 
                new List<ExitDefinition> { new(Direction.South, 1), new(Direction.North, 3) }),
            [3] = new(3, "Room 3", "Third room", 
                new List<ExitDefinition> { new(Direction.South, 2), new(Direction.North, 4) }),
            [4] = new(4, "Room 4", "Fourth room", 
                new List<ExitDefinition> { new(Direction.South, 3), new(Direction.North, 5) }),
            [5] = new(5, "Room 5", "Fifth room", 
                new List<ExitDefinition> { new(Direction.South, 4) })
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateDisconnectedWorld()
    {
        // Room 1 and Room 2 have no exits connecting them
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "First room", new List<ExitDefinition>()),
            [2] = new(2, "Room 2", "Second room", new List<ExitDefinition>())
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateDiamondWorld()
    {
        //     2
        //    / \
        //   1   4
        //    \ /
        //     3
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Center left", 
                new List<ExitDefinition> { new(Direction.North, 2), new(Direction.South, 3) }),
            [2] = new(2, "Room 2", "Top", 
                new List<ExitDefinition> { new(Direction.South, 1), new(Direction.East, 4) }),
            [3] = new(3, "Room 3", "Bottom", 
                new List<ExitDefinition> { new(Direction.North, 1), new(Direction.East, 4) }),
            [4] = new(4, "Room 4", "Center right", 
                new List<ExitDefinition> { new(Direction.West, 2), new(Direction.West, 3) })
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateSquareWorld()
    {
        //   1 --- 2
        //   |  X  |
        //   3 --- 4
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Top left", 
                new List<ExitDefinition> { new(Direction.East, 2), new(Direction.South, 3) }),
            [2] = new(2, "Room 2", "Top right", 
                new List<ExitDefinition> { new(Direction.West, 1), new(Direction.South, 4) }),
            [3] = new(3, "Room 3", "Bottom left", 
                new List<ExitDefinition> { new(Direction.North, 1), new(Direction.East, 4) }),
            [4] = new(4, "Room 4", "Bottom right", 
                new List<ExitDefinition> { new(Direction.West, 3), new(Direction.North, 2) })
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateWorldWithNoMobRoom()
    {
        // 1 -> 2 (NO_MOB) -> 3
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "First room", 
                new List<ExitDefinition> { new(Direction.North, 2) }),
            [2] = new(2, "Room 2", "No mob room", 
                new List<ExitDefinition> { new(Direction.South, 1), new(Direction.North, 3) },
                RoomFlags.NoMob),
            [3] = new(3, "Room 3", "Third room", 
                new List<ExitDefinition> { new(Direction.South, 2) })
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateWorldWithDeathRoom()
    {
        // 1 -> 2 (DEATH) -> 3
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "First room", 
                new List<ExitDefinition> { new(Direction.North, 2) }),
            [2] = new(2, "Room 2", "Death trap", 
                new List<ExitDefinition> { new(Direction.South, 1), new(Direction.North, 3) },
                RoomFlags.Death),
            [3] = new(3, "Room 3", "Third room", 
                new List<ExitDefinition> { new(Direction.South, 2) })
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateMultiZoneWorld()
    {
        // Room 1 (zone 1) -> Room 2 (zone 2)
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Room 1", "Zone 1 room", 
                new List<ExitDefinition> { new(Direction.North, 2) },
                ZoneId: 1),
            [2] = new(2, "Room 2", "Zone 2 room", 
                new List<ExitDefinition> { new(Direction.South, 1) },
                ZoneId: 2)
        };

        return CreateWorldState(rooms);
    }

    private WorldState CreateWorldState(Dictionary<int, RoomDefinition> rooms)
    {
        var worldDef = new WorldDefinition(rooms);
        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>>();
        var roomObjs = new Dictionary<int, List<ObjectInstance>>();
        var zones = new List<ZoneDefinition>();

        return new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);
    }
}

using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Server;

public class WorldStateShuffleExitsTests
{
    private const int MazeRoom = 15465;

    [Fact]
    public void ResetZone_ShuffleExits_KeepsEveryExitAndOnlyMovesFirstDirections()
    {
        // Legacy 154.zon: "R 0 15465 4" shuffles north, east, south and west
        var worldState = CreateWorld(exitCount: 4);

        for (var reset = 0; reset < 20; reset++)
        {
            worldState.ResetZone(154);

            var exits = worldState.World.GetRoom(MazeRoom).Exits;
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, exits.Select(e => e.TargetRoomId).Order());
            Assert.Equal(5, exits.Select(e => e.Direction).Distinct().Count());
            Assert.Equal(5, exits.Single(e => e.Direction == Direction.Up).TargetRoomId);
        }
    }

    [Fact]
    public void ResetZone_ShuffleExits_ChangesTheLayoutOverRepeatedResets()
    {
        var worldState = CreateWorld(exitCount: 4);
        var layouts = new HashSet<string>();

        for (var reset = 0; reset < 50; reset++)
        {
            worldState.ResetZone(154);
            layouts.Add(string.Join(",", worldState.World.GetRoom(MazeRoom).Exits
                .OrderBy(e => e.Direction)
                .Select(e => e.TargetRoomId)));
        }

        // 24 possible layouts; 50 resets landing on one is practically impossible
        Assert.True(layouts.Count > 1);
    }

    [Fact]
    public void ResetZone_ShuffleExits_DoorStateMovesWithItsExit()
    {
        var worldState = CreateWorld(exitCount: 4, northIsDoor: true);

        for (var reset = 0; reset < 20; reset++)
        {
            worldState.ResetZone(154);

            var doorExit = worldState.World.GetRoom(MazeRoom).Exits.Single(e => e.TargetRoomId == 1);
            Assert.True(doorExit.IsDoor);
            Assert.NotNull(worldState.GetDoorState(MazeRoom, doorExit.Direction));

            foreach (var exit in worldState.World.GetRoom(MazeRoom).Exits.Where(e => !e.IsDoor))
            {
                Assert.Null(worldState.GetDoorState(MazeRoom, exit.Direction));
            }
        }
    }

    private static WorldState CreateWorld(int exitCount, bool northIsDoor = false)
    {
        var exits = new List<ExitDefinition>
        {
            new(Direction.North, 1, ExitFlags: northIsDoor ? new[] { "IsDoor" } : null),
            new(Direction.East, 2),
            new(Direction.South, 3),
            new(Direction.West, 4),
            new(Direction.Up, 5)
        };

        var rooms = new Dictionary<int, RoomDefinition>
        {
            [MazeRoom] = new(MazeRoom, "The House", "Desc", exits)
        };
        foreach (var target in new[] { 1, 2, 3, 4, 5 })
        {
            rooms[target] = new(target, $"Room {target}", "Desc", new List<ExitDefinition>());
        }

        var zone = new ZoneDefinition(
            154, "The Amazeing Maze", new RoomRange(15400, 15499), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                new("ShuffleExits", ObjectId: null, MobId: null, RoomId: MazeRoom, MaxExisting: null,
                    SpawnChance: null, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
                    IfFlag: false, ExitCount: exitCount)
            });

        return new WorldState(
            new WorldDefinition(rooms),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            rooms.Keys.ToDictionary(id => id, _ => new List<MobInstance>()),
            rooms.Keys.ToDictionary(id => id, _ => new List<ObjectInstance>()),
            new List<ZoneDefinition> { zone });
    }
}

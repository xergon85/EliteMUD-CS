using EliteMud.Application.Commands.Move;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Server;

public class WorldStateDoorStateTests
{
    private const int Room = 6461;
    private const int Next = 6460;

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    public void ResetZone_DoorState_SetsClosedAndLocked(int state, bool closed, bool locked)
    {
        var worldState = CreateWorld(DoorExit(), DoorReset(state));

        worldState.ResetZone(64);

        var door = worldState.GetDoorState(Room, Direction.East)!;
        Assert.Equal(closed, door.IsClosed);
        Assert.Equal(locked, door.IsLocked);
        Assert.Equal(closed, worldState.IsExitClosed(Room, Direction.East));
    }

    [Fact]
    public void ResetZone_DoorState_RepairsABashedDoor()
    {
        var worldState = CreateWorld(DoorExit(), DoorReset(1));
        worldState.BreakDoor(Room, Direction.East);

        worldState.ResetZone(64);

        Assert.False(worldState.GetDoorState(Room, Direction.East)!.IsBroken);
        Assert.True(worldState.IsExitClosed(Room, Direction.East));
    }

    [Fact]
    public void ResetZone_DoorStateOnPlainExit_MakesABarrier()
    {
        // Legacy 'D' works on any exit; e.g. the Desert Moongate's up exit isn't a door
        var worldState = CreateWorld(new ExitDefinition(Direction.East, Next, Keywords: new[] { "moongate" }), DoorReset(2));

        worldState.ResetZone(64);

        Assert.True(worldState.IsExitClosed(Room, Direction.East));
    }

    [Fact]
    public void ResetZone_DoorStateOnMissingExit_FailsTheIfFlagChain()
    {
        // Closes a north exit the room doesn't have, then a load with the if-flag must be skipped
        var worldState = CreateWorld(
            DoorExit(),
            new ZoneResetDefinition("DoorState", null, null, Room, null, null, null, null, DoorDirection: 0, DoorState: 1, IfFlag: false),
            new ZoneResetDefinition("ShuffleExits", null, null, Room, null, null, null, null, null, null, IfFlag: false),
            new ZoneResetDefinition("DoorState", null, null, Room, null, null, null, null, DoorDirection: 1, DoorState: 1, IfFlag: true));

        worldState.ResetZone(64);

        Assert.False(worldState.IsExitClosed(Room, Direction.East)); // skipped: the previous door reset failed
    }

    [Fact]
    public void Move_ClosedDoor_IsBlockedWithItsKeyword()
    {
        var worldState = CreateWorld(DoorExit(), DoorReset(1));
        worldState.ResetZone(64);
        var player = new PlayerState(1, "Tester", Room);

        var result = new MoveHandler(worldState).Handle(player, Direction.East);

        Assert.False(result.Moved);
        Assert.Equal("The door seems to be closed.", result.Message);
        Assert.Equal(Room, player.RoomId);
    }

    [Fact]
    public void Move_ClosedSecretExit_DoesNotRevealItself()
    {
        var secret = new ExitDefinition(Direction.East, Next, Keywords: new[] { "panel" }, ExitFlags: new[] { "IsDoor", "Secret" });
        var worldState = CreateWorld(secret, DoorReset(1));
        worldState.ResetZone(64);

        var result = new MoveHandler(worldState).Handle(new PlayerState(1, "Tester", Room), Direction.East);

        Assert.Equal("Alas, you cannot go that way...", result.Message);
    }

    private static ExitDefinition DoorExit() =>
        new(Direction.East, Next, "A bright green door.", new[] { "door" }, new[] { "IsDoor" });

    private static ZoneResetDefinition DoorReset(int state) =>
        new("DoorState", null, null, Room, null, null, null, null, DoorDirection: (int)Direction.East, DoorState: state, IfFlag: false);

    private static WorldState CreateWorld(ExitDefinition exit, params ZoneResetDefinition[] resets)
    {
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [Room] = new(Room, "The Playroom", "Desc", new[] { exit }),
            [Next] = new(Next, "A Hallway", "Desc", Array.Empty<ExitDefinition>())
        };
        var zone = new ZoneDefinition(64, "Great Underground Dwarven Empire", new RoomRange(6400, 6499), "ResetAlways", resets);

        return new WorldState(
            new WorldDefinition(rooms),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            rooms.Keys.ToDictionary(id => id, _ => new List<MobInstance>()),
            rooms.Keys.ToDictionary(id => id, _ => new List<ObjectInstance>()),
            new List<ZoneDefinition> { zone });
    }
}

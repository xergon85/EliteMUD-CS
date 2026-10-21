using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Server;

/// <summary>
/// A mob knows the room it's in (legacy ch->in_room), and the world keeps that in step with its rooms' mob lists.
/// </summary>
public class WorldStateMobRoomTests
{
    private const int First = 1;
    private const int Second = 2;

    private readonly MobDefinition _definition = new(
        100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
        new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
        new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);

    [Fact]
    public void AMobLoadedWithTheWorld_KnowsItsRoom()
    {
        var mob = new MobInstance(1, _definition);

        CreateWorld(mob);

        Assert.Equal(Second, mob.RoomId);
    }

    [Fact]
    public void AMobOutsideTheWorld_IsNowhere()
    {
        Assert.Equal(MobInstance.Nowhere, new MobInstance(1, _definition).RoomId);
    }

    [Fact]
    public void AMobSpawnedByAZoneReset_KnowsItsRoom()
    {
        var world = CreateWorld();

        world.ResetZone(1);

        var mob = Assert.Single(world.Population.MobsIn(Second));
        Assert.Equal(Second, mob.RoomId);
    }

    [Fact]
    public void AMobMoved_KnowsItsNewRoom()
    {
        var mob = new MobInstance(1, _definition);
        var world = CreateWorld(mob);

        world.Population.Move(mob, First);

        Assert.Equal(First, mob.RoomId);
    }

    [Fact]
    public void AMobRemoved_IsNowhere()
    {
        var mob = new MobInstance(1, _definition);
        var world = CreateWorld(mob);

        world.Population.Leave(mob);

        Assert.Equal(MobInstance.Nowhere, mob.RoomId);
    }

    [Fact]
    public void AMobInTheZone_StaysInItsRoomThroughAReset()
    {
        var mob = new MobInstance(1, _definition);
        var world = CreateWorld(mob);

        world.ResetZone(1);

        Assert.Equal(Second, mob.RoomId);
        Assert.Equal(new[] { mob }, world.Population.MobsIn(Second));
    }

    /// <summary>Two rooms in zone 1, whose reset loads one rat into the second room.</summary>
    private WorldState CreateWorld(MobInstance? inSecondRoom = null)
    {
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [First] = new(First, "First", "Desc", new List<ExitDefinition>()),
            [Second] = new(Second, "Second", "Desc", new List<ExitDefinition>())
        };
        var zone = new ZoneDefinition(
            1, "Zone", new RoomRange(First, Second), "ResetAlways",
            new List<ZoneResetDefinition>
            {
                new("LoadMob", ObjectId: null, MobId: _definition.Id, RoomId: Second, MaxExisting: 1,
                    SpawnChance: 100, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
                    IfFlag: false)
            });

        return new WorldState(
            new WorldDefinition(rooms),
            new Dictionary<int, MobDefinition> { [_definition.Id] = _definition },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>>
            {
                [First] = new(),
                [Second] = inSecondRoom is null ? new() : new() { inSecondRoom }
            },
            new Dictionary<int, List<ObjectInstance>> { [First] = new(), [Second] = new() },
            new List<ZoneDefinition> { zone });
    }
}

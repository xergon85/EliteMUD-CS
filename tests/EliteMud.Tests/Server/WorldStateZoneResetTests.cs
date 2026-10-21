using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Server;

/// <summary>
/// A zone reset as legacy reset_zone does it (#4): mobs stay, objects are swept only from rooms without players
/// (clear_eq_zone), and a mob or object loads only when it's missing.
/// </summary>
public class WorldStateZoneResetTests
{
    private const int First = 1;
    private const int Second = 2;
    private const int Third = 3;

    private readonly MobDefinition _rat = new(
        100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
        new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
        new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);

    private readonly ObjectDefinition _bread = Object(200, "bread");
    private readonly ObjectDefinition _statue = Object(201, "statue", "NoSweep");
    private readonly ObjectDefinition _sword = Object(202, "sword");
    private int _nextObjectId = 1000;

    [Fact]
    public void AReset_KeepsTheZonesMobs_AndTheirFights()
    {
        var rat = new MobInstance(1, _rat);
        var player = Player();
        var world = CreateWorld(new() { LoadMob(Second) }, startingRat: rat);
        world.Population.Enter(player, Second);
        rat.Fighting = player;
        player.Fighting = rat;

        world.ResetZone(1);

        Assert.Equal(new[] { rat }, world.Population.MobsIn(Second));
        Assert.Same(player, rat.Fighting);
        Assert.Same(rat, player.Fighting);
    }

    /// <summary>Legacy counts a mob's copies in the whole world (mob_index[].number), not in its reset room.</summary>
    [Fact]
    public void AMobThatWanderedOff_IsNotLoadedAgain_ButOneThatDied_Is()
    {
        var rat = new MobInstance(1, _rat);
        var world = CreateWorld(new() { LoadMob(Second) }, startingRat: rat);
        world.Population.Move(rat, First);

        world.ResetZone(1);

        Assert.Empty(world.Population.MobsIn(Second));

        world.Population.Leave(rat);
        world.ResetZone(1);

        var reloaded = Assert.Single(world.Population.MobsIn(Second));
        Assert.NotSame(rat, reloaded);
    }

    [Fact]
    public void AReset_SweepsObjectsFromRoomsWithoutPlayers_ButNotNoSweepRoomsOrObjects()
    {
        var bread = Instance(_bread);
        var statue = Instance(_statue);
        var guarded = Instance(_bread);
        var world = CreateWorld(new(), objects: new()
        {
            [First] = new() { bread, statue },
            [Second] = new() { guarded }
        }, secondRoomFlags: RoomFlags.NoSweep);

        world.ResetZone(1);

        Assert.Equal(new[] { statue }, world.GetObjectsInRoom(First));
        Assert.Equal(new[] { guarded }, world.GetObjectsInRoom(Second));
    }

    [Fact]
    public void AnItemAPlayerDropped_StaysWhileTheyAreInTheRoom()
    {
        var dropped = Instance(_sword);
        var world = CreateWorld(new(), objects: new() { [Third] = new() { dropped } });
        world.Population.Enter(Player(), Third);

        world.ResetZone(1);

        Assert.Equal(new[] { dropped }, world.GetObjectsInRoom(Third));
    }

    /// <summary>
    /// Legacy never clears clear_eq_zone's pc_inroom, so a player in a room with objects keeps the zone's later rooms
    /// from being swept too. A player in a room without objects isn't noticed.
    /// </summary>
    [Fact]
    public void APlayerInARoomWithObjects_KeepsTheLaterRoomsFromBeingSwept_AsLegacyDoes()
    {
        var first = Instance(_bread);
        var second = Instance(_bread);
        var third = Instance(_bread);
        var world = CreateWorld(new(), objects: new()
        {
            [First] = new() { first },
            [Second] = new() { second },
            [Third] = new() { third }
        });
        world.Population.Enter(Player(), Second);

        world.ResetZone(1);

        Assert.Empty(world.GetObjectsInRoom(First));
        Assert.Equal(new[] { second }, world.GetObjectsInRoom(Second));
        Assert.Equal(new[] { third }, world.GetObjectsInRoom(Third));
    }

    [Fact]
    public void APlayerInARoomWithoutObjects_DoesNotKeepLaterRoomsFromBeingSwept()
    {
        var world = CreateWorld(new(), objects: new() { [Second] = new() { Instance(_bread) } });
        world.Population.Enter(Player(), First);

        world.ResetZone(1);

        Assert.Empty(world.GetObjectsInRoom(Second));
    }

    /// <summary>
    /// An object already in the room isn't loaded again (legacy get_obj_in_list_num), and the next if-flag command
    /// goes by the command before it, since legacy leaves last_cmd as it was.
    /// </summary>
    [Fact]
    public void AnObjectAlreadyInTheRoom_IsNotLoadedAgain_AndLeavesTheIfFlagAsItWas()
    {
        var existing = Instance(_bread);
        var world = CreateWorld(
            new() { LoadMob(Second), LoadObject(_bread, Third, ifFlag: false), LoadObject(_sword, Third, ifFlag: true) },
            objects: new() { [Third] = new() { existing } });
        world.Population.Enter(Player(), Third);

        world.ResetZone(1);

        Assert.Equal(new[] { _bread.Id, _sword.Id }, world.GetObjectsInRoom(Third).Select(obj => obj.Definition.Id));
        Assert.Same(existing, world.GetObjectsInRoom(Third)[0]);
    }

    [Fact]
    public void AnObjectAlreadyInTheRoom_AfterAFailedCommand_KeepsTheNextIfFlagCommandFromRunning()
    {
        var world = CreateWorld(
            new() { LoadMob(Second), LoadObject(_bread, Third, ifFlag: false), LoadObject(_sword, Third, ifFlag: true) },
            startingRat: new MobInstance(1, _rat),
            objects: new() { [Third] = new() { Instance(_bread) } });
        world.Population.Enter(Player(), Third);

        world.ResetZone(1);

        Assert.Equal(new[] { _bread.Id }, world.GetObjectsInRoom(Third).Select(obj => obj.Definition.Id));
    }

    private WorldState CreateWorld(
        List<ZoneResetDefinition> resets,
        MobInstance? startingRat = null,
        Dictionary<int, List<ObjectInstance>>? objects = null,
        RoomFlags secondRoomFlags = RoomFlags.None)
    {
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [First] = new(First, "First", "Desc", new List<ExitDefinition>()),
            [Second] = new(Second, "Second", "Desc", new List<ExitDefinition>(), secondRoomFlags),
            [Third] = new(Third, "Third", "Desc", new List<ExitDefinition>())
        };
        var roomObjects = new Dictionary<int, List<ObjectInstance>> { [First] = new(), [Second] = new(), [Third] = new() };
        foreach (var (roomId, list) in objects ?? new())
        {
            roomObjects[roomId] = list;
        }

        return new WorldState(
            new WorldDefinition(rooms),
            new Dictionary<int, MobDefinition> { [_rat.Id] = _rat },
            new Dictionary<int, ObjectDefinition> { [_bread.Id] = _bread, [_statue.Id] = _statue, [_sword.Id] = _sword },
            new Dictionary<int, List<MobInstance>>
            {
                [First] = new(),
                [Second] = startingRat is null ? new() : new() { startingRat },
                [Third] = new()
            },
            roomObjects,
            new List<ZoneDefinition> { new(1, "Zone", new RoomRange(First, Third), "ResetAlways", resets) });
    }

    private ZoneResetDefinition LoadMob(int roomId) =>
        new("LoadMob", ObjectId: null, MobId: _rat.Id, RoomId: roomId, MaxExisting: 1,
            SpawnChance: null, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
            IfFlag: false);

    private static ZoneResetDefinition LoadObject(ObjectDefinition definition, int roomId, bool ifFlag) =>
        new("LoadObject", ObjectId: definition.Id, MobId: null, RoomId: roomId, MaxExisting: null,
            SpawnChance: 100, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
            IfFlag: ifFlag);

    private ObjectInstance Instance(ObjectDefinition definition) => new(_nextObjectId++, definition);

    private static ObjectDefinition Object(int id, string name, params string[] flags) => new(
        id, name, $"a {name}", "Long", "Desc", "Other",
        new List<string>(), flags.ToList(), null, new List<int>(), 1, 1, new List<ObjectAffect>());

    private static PlayerState Player() => new(1, "Tester", First, level: 5, "warrior", "human", sex: 1);
}

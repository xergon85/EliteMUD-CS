using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Game;

/// <summary>
/// Mobs in the world's population (#79): legacy bubble_char_to_list's room order, and char_from_room and
/// extract_char's rules for fights.
/// </summary>
public class WorldPopulationMobTests
{
    private const int Temple = 1;
    private const int Square = 2;

    private readonly MobDefinition _rat = Definition(100, "a rat");
    private readonly MobDefinition _cat = Definition(200, "a cat");
    private readonly MobInstance _firstRat;
    private readonly WorldState _world;
    private int _nextId = 1;

    public WorldPopulationMobTests()
    {
        _firstRat = Mob(_rat);
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                [Square] = new(Square, "Square", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [_rat.Id] = _rat, [_cat.Id] = _cat },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Temple] = new() { _firstRat }, [Square] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Temple] = new(), [Square] = new() },
            new List<ZoneDefinition>());
    }

    private WorldPopulation Population => _world.Population;

    [Fact]
    public void Entering_PutsANewMobAtTheHead_AndATwinInFrontOfItsTwin()
    {
        var cat = Mob(_cat);
        var secondRat = Mob(_rat);

        Population.Enter(cat, Temple);
        Population.Enter(secondRat, Temple);

        Assert.Equal(new[] { cat, secondRat, _firstRat }, Population.MobsIn(Temple));
        Assert.Equal(Temple, secondRat.RoomId);
    }

    /// <summary>
    /// Twins have the same affected_by flags too. Sanctuary sets one; armor doesn't, so it doesn't split them.
    /// </summary>
    [Fact]
    public void Entering_NextToAMobWithAnotherFlag_GoesAtTheHead_ButArmorDoesNotCount()
    {
        var cat = Mob(_cat);
        Population.Enter(cat, Temple);
        _firstRat.AddAffect(Affect(AffectType.Sanctuary));

        var plainRat = Mob(_rat);
        Population.Enter(plainRat, Temple);

        Assert.Equal(new[] { plainRat, cat, _firstRat }, Population.MobsIn(Temple));

        var armoredCat = Mob(_cat);
        armoredCat.AddAffect(Affect(AffectType.Armor));
        Population.Enter(armoredCat, Temple);

        Assert.Equal(new[] { plainRat, armoredCat, cat, _firstRat }, Population.MobsIn(Temple));
    }

    [Fact]
    public void StartingMobs_KeepTheOrderTheyWereGivenIn()
    {
        var cat = Mob(_cat);
        var rat = Mob(_rat);
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Temple] = new() { cat, rat } },
            new Dictionary<int, List<ObjectInstance>> { [Temple] = new() },
            new List<ZoneDefinition>());

        Assert.Equal(new[] { cat, rat }, world.Population.MobsIn(Temple));
    }

    [Fact]
    public void AStartingMobListedTwice_Fails()
    {
        var rat = Mob(_rat);

        Assert.Throws<InvalidOperationException>(() => new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                [Square] = new(Square, "Square", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Temple] = new() { rat }, [Square] = new() { rat } },
            new Dictionary<int, List<ObjectInstance>> { [Temple] = new(), [Square] = new() },
            new List<ZoneDefinition>()));
    }

    [Fact]
    public void Entering_TwiceOrIntoNoRoom_Fails()
    {
        Assert.Throws<InvalidOperationException>(() => Population.Enter(_firstRat, Square));

        var lost = Mob(_rat);
        Assert.Throws<ArgumentException>(() => Population.Enter(lost, 999));
        Assert.Equal(MobInstance.Nowhere, lost.RoomId);
        Assert.DoesNotContain(lost, Population.Mobs);
    }

    /// <summary>Leaving a room stops the mob's fight (legacy char_from_room); its opponent's isn't stopped.</summary>
    [Fact]
    public void Moving_StopsTheMobsFight_AndPutsItInTheNewRoom()
    {
        var player = Player();
        Population.Enter(player, Temple);
        _firstRat.Fighting = player;
        _firstRat.Position = Position.Fighting;
        player.Fighting = _firstRat;

        Population.Move(_firstRat, Square);

        Assert.Null(_firstRat.Fighting);
        Assert.Equal(Position.Standing, _firstRat.Position);
        Assert.Same(_firstRat, player.Fighting);
        Assert.Empty(Population.MobsIn(Temple));
        Assert.Equal(new[] { _firstRat }, Population.MobsIn(Square));
        Assert.Equal(Square, _firstRat.RoomId);
    }

    [Fact]
    public void Moving_IntoNoRoom_LeavesTheMobWhereItWas()
    {
        var player = Player();
        _firstRat.Fighting = player;

        Assert.Throws<ArgumentException>(() => Population.Move(_firstRat, 999));

        Assert.Equal(new[] { _firstRat }, Population.MobsIn(Temple));
        Assert.Same(player, _firstRat.Fighting);
    }

    /// <summary>A mob leaving the world stops its fight and everyone's fighting it (legacy extract_char).</summary>
    [Fact]
    public void Leaving_TakesTheMobOut_AndStopsEveryoneFightingIt()
    {
        var player = Player();
        Population.Enter(player, Temple);
        var cat = Mob(_cat);
        Population.Enter(cat, Temple);
        _firstRat.Fighting = player;
        player.Fighting = _firstRat;
        player.Position = Position.Fighting;
        cat.Fighting = _firstRat;

        Population.Leave(_firstRat);

        Assert.Equal(MobInstance.Nowhere, _firstRat.RoomId);
        Assert.DoesNotContain(_firstRat, Population.MobsIn(Temple));
        Assert.DoesNotContain(_firstRat, Population.Mobs);
        Assert.Null(_firstRat.Fighting);
        Assert.Null(player.Fighting);
        Assert.Null(cat.Fighting);
    }

    /// <summary>A zone reset that loads two of the same mob puts the second in front of the first.</summary>
    [Fact]
    public void AZoneReset_LoadsTwinsTogether_NewestFirst()
    {
        var zone = new ZoneDefinition(
            1, "Zone", new RoomRange(Temple, Square), "ResetAlways",
            new List<ZoneResetDefinition> { LoadMob(_rat, Square), LoadMob(_cat, Square), LoadMob(_rat, Square) });
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                [Square] = new(Square, "Square", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [_rat.Id] = _rat, [_cat.Id] = _cat },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>>(),
            new Dictionary<int, List<ObjectInstance>>(),
            new List<ZoneDefinition> { zone });

        world.ResetZone(1);

        Assert.Equal(
            new[] { "a cat", "a rat", "a rat" },
            world.Population.MobsIn(Square).Select(mob => mob.Name));
        var mobs = world.Population.MobsIn(Square);
        Assert.True(mobs[1].InstanceId > mobs[2].InstanceId);
    }

    /// <summary>
    /// Room 542's reset loads mobs 511, 511, 520, 520 and 540 in that order; legacy lists the last first and keeps
    /// the twins together.
    /// </summary>
    [Fact]
    public void TheRealWorld_ListsARoomsMobsInLegacyOrder()
    {
        Assert.Equal(
            new[] { 540, 520, 520, 511, 511 },
            RealWorld.State.Population.MobsIn(542).Select(mob => mob.Definition.Id));
    }

    private static ZoneResetDefinition LoadMob(MobDefinition definition, int roomId) =>
        new("LoadMob", ObjectId: null, MobId: definition.Id, RoomId: roomId, MaxExisting: 2,
            SpawnChance: 100, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
            IfFlag: false);

    private MobInstance Mob(MobDefinition definition) => new(_nextId++, definition);

    private static MobDefinition Definition(int id, string shortDescription) => new(
        id, shortDescription, shortDescription, "It is here.", "Desc", 5, "Animal", "None",
        new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
        new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);

    private static Affect Affect(AffectType type) =>
        new() { Type = type, Location = AffectLocation.None, Modifier = 0, DurationHours = 5 };

    private static PlayerState Player() => new(1, "Tester", Temple, level: 5, "warrior", "human", sex: 1);
}

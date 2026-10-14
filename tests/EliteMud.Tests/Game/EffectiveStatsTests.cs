using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Game;

/// <summary>
/// The one place effective stats are worked out (#80): base + affects + gear, for players and mobs, within legacy's
/// limits (stat_check; #88). A human's ability maximum is 18; a mob's is 25.
/// </summary>
public class EffectiveStatsTests
{
    private const int Room = 3002;
    private const int Gauntlets = 300;
    private const int Plate = 301;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");
    private static readonly RulesContent Rules = RulesContentLoader.Load(Content).Rules;

    private readonly WorldState _world;
    private readonly EffectiveStats _stats;
    private readonly PlayerState _player;
    private readonly MobInstance _mob;

    public EffectiveStatsTests()
    {
        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 11, 12, 13, 14, 15),
            new List<string>(), new List<string>(), 40, 100, 0, new List<MobAttack>(), new MobCombat(3, 4));
        _mob = new MobInstance(1, mobDefinition);

        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Room] = new(Room, "Arena", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = mobDefinition },
            new Dictionary<int, ObjectDefinition>
            {
                [Gauntlets] = Item(Gauntlets, "gauntlets",
                    new ObjectAffect(AffectLocation.Strength, 2), new ObjectAffect(AffectLocation.Hitroll, 1)),
                [Plate] = Item(Plate, "plate",
                    new ObjectAffect(AffectLocation.ArmorClass, -30), new ObjectAffect(AffectLocation.SavingMagic, -2))
            },
            new Dictionary<int, List<MobInstance>> { [Room] = new() { _mob } },
            new Dictionary<int, List<ObjectInstance>> { [Room] = new() },
            new List<ZoneDefinition>());
        _stats = new EffectiveStats(_world, Rules);

        _player = new PlayerState(1, "Tester", Room, level: 30, "warrior", "human", sex: 1)
        {
            Strength = 14,
            Hitroll = 2,
            ArmorClass = 100,
            MaxHitPoints = 50,
            MaxMana = 20
        };
    }

    [Fact]
    public void APlayersStat_IsBasePlusAffectsPlusGear()
    {
        Wear(Gauntlets, EquipmentSlot.Hands);
        _player.AddAffect(Affect(AffectLocation.Strength, 1));

        Assert.Equal(14 + 1 + 2, _stats.Get(_player, AffectLocation.Strength));
        Assert.Equal(1, _stats.AffectBonus(_player, AffectLocation.Strength));
        Assert.Equal(2, _stats.GearBonus(_player, AffectLocation.Strength));
        Assert.Equal(2 + 1, _stats.Get(_player, AffectLocation.Hitroll));
    }

    /// <summary>Legacy stat_check: a player's ability stops at the race's maximum and never goes below 0.</summary>
    [Theory]
    [InlineData("human", 6, 18)]
    [InlineData("troll", 6, 20)]
    [InlineData("human", -20, 0)] // below the old floor of 3
    [InlineData("no-such-race", 20, 25)]
    public void APlayersAbility_StaysWithinTheirRacesLimits(string race, int modifier, int strength)
    {
        _player.Race = race;
        _player.AddAffect(Affect(AffectLocation.Strength, modifier));

        Assert.Equal(strength, _stats.Get(_player, AffectLocation.Strength));
    }

    /// <summary>Gear can't take a human past 18 either (#88): 17 with two pairs of +2 gauntlets is 18.</summary>
    [Fact]
    public void Gear_CantTakeAHumanPastTheirRacesMaximum()
    {
        _player.Strength = 17;
        Wear(Gauntlets, EquipmentSlot.Hands);
        Wear(Gauntlets, EquipmentSlot.Arms);

        Assert.Equal(4, _stats.GearBonus(_player, AffectLocation.Strength));
        Assert.Equal(18, _stats.Get(_player, AffectLocation.Strength));
    }

    [Fact]
    public void AMobsStat_IsItsContentPlusAffectsPlusGear_Within0To25()
    {
        _mob.Equip(_world.CreateObjectInstance(Gauntlets)!, EquipmentSlot.Hands);
        _mob.AddAffect(Affect(AffectLocation.Hitroll, 2));

        Assert.Equal(10 + 2, _stats.Get(_mob, AffectLocation.Strength));
        Assert.Equal(11, _stats.Get(_mob, AffectLocation.Dexterity));
        Assert.Equal(15, _stats.Get(_mob, AffectLocation.Charisma));
        Assert.Equal(3 + 2 + 1, _stats.Get(_mob, AffectLocation.Hitroll));
        Assert.Equal(4, _stats.Get(_mob, AffectLocation.Damroll));
        Assert.Equal(100, _stats.Get(_mob, AffectLocation.MaxHit));

        _mob.AddAffect(Affect(AffectLocation.Strength, 40, AffectType.Curse));
        Assert.Equal(25, _stats.Get(_mob, AffectLocation.Strength));
    }

    /// <summary>Armor class counts both the Armor and ArmorClass locations; lower is better.</summary>
    [Fact]
    public void ArmorClass_CountsBothArmorLocations_FromAffectsAndGear()
    {
        Wear(Plate, EquipmentSlot.Body);
        _player.AddAffect(Affect(AffectLocation.Armor, -20));

        Assert.Equal(100 - 30 - 20, _stats.Get(_player, AffectLocation.ArmorClass));
        Assert.Equal(40, _stats.Get(_mob, AffectLocation.ArmorClass));
    }

    [Fact]
    public void MaxHitPoints_AreAtLeastOne_AndManaAtLeastZero()
    {
        _player.AddAffect(Affect(AffectLocation.MaxHit, -500));
        _player.AddAffect(Affect(AffectLocation.MaxMana, -500, AffectType.Curse)); // one affect per type

        Assert.Equal(1, _stats.Get(_player, AffectLocation.MaxHit));
        Assert.Equal(0, _stats.Get(_player, AffectLocation.MaxMana));
    }

    /// <summary>Saving throws have no base value here: only affects and gear count.</summary>
    [Fact]
    public void ASavingThrow_IsAffectsPlusGear()
    {
        Wear(Plate, EquipmentSlot.Body);
        _player.AddAffect(Affect(AffectLocation.SavingMagic, -1));

        Assert.Equal(-3, _stats.Get(_player, AffectLocation.SavingMagic));
    }

    /// <summary>Legacy stat_check caps the 18/xx bonus at 100; mobs have none.</summary>
    [Fact]
    public void ExceptionalStrength_IsAtMost100()
    {
        _player.StrengthAdd = 120;

        Assert.Equal(100, _stats.StrengthAdd(_player));
        Assert.Equal(0, _stats.StrengthAdd(_mob));
    }

    /// <summary>Legacy get_thaco(): from the class (a level 30 warrior: 20 − 30 / 3); 20 for a mob or no class.</summary>
    [Fact]
    public void Thac0_ComesFromTheClass()
    {
        Assert.Equal(10, _stats.Thac0(_player));
        Assert.Equal(20, _stats.Thac0(_mob));
        Assert.Equal(20, _stats.Thac0(new PlayerState(2, "Nobody", Room, level: 30, "no-such-class")));
    }

    private void Wear(int objectId, EquipmentSlot slot) =>
        _player.EquipToSlot((int)slot, _world.CreateObjectInstance(objectId)!.InstanceId);

    private static Affect Affect(AffectLocation location, int modifier, AffectType type = AffectType.Bless) => new()
    {
        Type = type,
        Location = location,
        Modifier = modifier,
        DurationHours = 10
    };

    private static ObjectDefinition Item(int id, string name, params ObjectAffect[] affects) =>
        new(id, name, name, $"A {name} lies here.", "Desc", "Armor",
            new List<string>(), new List<string>(), null, new List<int>(), 10, 100, affects.ToList());
}

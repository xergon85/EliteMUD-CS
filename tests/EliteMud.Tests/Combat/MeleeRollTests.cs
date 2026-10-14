using EliteMud.Application.Combat;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Ai;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Combat;

/// <summary>
/// Legacy hit() up to damage(): whether a melee attack lands and how much it does, with every stat the effective one
/// (base + spells + gear; #78).
/// The to-hit numbers: a level 1 attacker with 13 in every stat needs THAC0 19 − victim AC / 10. A victim with AC 100
/// and dexterity 12 is at (100 − 20) / 10 = 8, so a roll of 11 or more hits.
/// </summary>
public class MeleeRollTests
{
    private const int Room = 3002;
    private const int Plate = 201;
    private const int RingOfDamage = 202;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly WorldState _world;
    private readonly PlayerState _attacker;
    private readonly PlayerState _victim;
    private readonly MobInstance _mob;

    public MeleeRollTests()
    {
        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 100, 0,
            new List<MobAttack> { new("bite", 0, 100, 2, 4, 1) }, null);
        _mob = new MobInstance(1, mobDefinition);

        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Room] = new(Room, "Arena", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = mobDefinition },
            new Dictionary<int, ObjectDefinition>
            {
                [Plate] = Worn(Plate, "plate", new ObjectAffect(AffectLocation.ArmorClass, -100)),
                [RingOfDamage] = Worn(RingOfDamage, "ring", new ObjectAffect(AffectLocation.Damroll, 5))
            },
            new Dictionary<int, List<MobInstance>> { [Room] = new() { _mob } },
            new Dictionary<int, List<ObjectInstance>> { [Room] = new() },
            new List<ZoneDefinition>());

        _attacker = Player(1, "Attacker");
        _victim = Player(2, "Victim");
        _victim.Dexterity = 12;
    }

    [Fact]
    public void ABareVictim_IsHit()
    {
        Assert.True(Roll(15).Hits(_attacker, _victim));
    }

    [Fact]
    public void WornArmor_MakesAHitMiss()
    {
        Wear(_victim, Plate, EquipmentSlot.Body); // AC 0: (0 − 20) / 10 = −2, so only a natural 20 hits

        Assert.False(Roll(15).Hits(_attacker, _victim));
    }

    [Fact]
    public void AnArmorSpell_MakesAHitMiss()
    {
        _victim.AddAffect(new Affect
        {
            Type = AffectType.Armor,
            Location = AffectLocation.Armor,
            Modifier = -60, // AC 40: (40 − 20) / 10 = 2, so 17 or more hits
            DurationHours = 10
        });

        Assert.False(Roll(15).Hits(_attacker, _victim));
    }

    [Fact]
    public void ABlessSpellsHitroll_Counts()
    {
        _attacker.AddAffect(new Affect
        {
            Type = AffectType.Bless,
            Location = AffectLocation.Hitroll,
            Modifier = 5, // THAC0 14: 6 or more hits
            DurationHours = 10
        });

        Assert.True(Roll(8).Hits(_attacker, _victim));
        Assert.False(Roll(8).Hits(Player(3, "Other"), _victim));
    }

    [Fact]
    public void ADamrollItem_AddsDamage()
    {
        Wear(_attacker, RingOfDamage, EquipmentSlot.FingerRight);

        Assert.Equal(5, Roll(1).Damage(_attacker, _victim)); // bare hands roll 0
    }

    /// <summary>Legacy str_app: 19 adds 7 to damage, and 18/xx counts its bonus.</summary>
    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(18, 0, 2)]
    [InlineData(18, 60, 3)]
    [InlineData(19, 0, 7)]
    public void Strength_AddsDamage(int strength, int strengthAdd, int damage)
    {
        _attacker.Strength = (sbyte)strength;
        _attacker.StrengthAdd = (sbyte)strengthAdd;

        Assert.Equal(damage, Roll(1).Damage(_attacker, _victim));
    }

    /// <summary>A mob's attack dice and damroll, plus its strength bonus (none at 10).</summary>
    [Fact]
    public void AMob_HitsWithItsAttackDice()
    {
        Assert.Equal(2 + 1, Roll(1).Damage(_mob, _victim)); // 2d4 rolling ones, + 1
    }

    private MeleeRoll Roll(int dice)
    {
        var skills = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        var formulas = new FormulaEvaluator(new Random(1));
        return new MeleeRoll(
            new CombatCalculator(new DodgeSkill(skills, formulas), new ParrySkill(skills, formulas), new FixedRandom(dice)),
            _world);
    }

    private void Wear(PlayerState player, int objectId, EquipmentSlot slot)
    {
        var item = _world.CreateObjectInstance(objectId)!;
        player.EquipToSlot((int)slot, item.InstanceId);
    }

    private static PlayerState Player(int id, string name) =>
        new(id, name, Room, level: 1, "warrior", "human", sex: 1)
        {
            Strength = 13,
            Intelligence = 13,
            Wisdom = 13,
            Dexterity = 13,
            Movement = 100,
            Position = Position.Standing
        };

    private static ObjectDefinition Worn(int id, string name, ObjectAffect affect) =>
        new(id, name, name, $"A {name} lies here.", "Desc", "Worn",
            new List<string>(), new List<string>(), null, new List<int>(), 1, 100, new List<ObjectAffect> { affect });
}

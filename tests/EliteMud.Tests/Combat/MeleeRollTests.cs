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
/// The to-hit numbers: a level 1 warrior with 13 in every stat has THAC0 20 (20 − 1 / 3) and no to-hit bonus. A victim
/// with AC 100 and dexterity 12 is at (100 − 20) / 10 = 8, so a roll of 12 or more hits.
/// </summary>
public class MeleeRollTests
{
    private const int Room = 3002;
    private const int Plate = 201;
    private const int RingOfDamage = 202;

    private const int Sword = 203;
    private const int Dagger = 204;
    private const int Wand = 205;
    private const int Axe = 206;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");
    private static readonly RulesContent Rules = RulesContentLoader.Load(Content).Rules;

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
                [RingOfDamage] = Worn(RingOfDamage, "ring", new ObjectAffect(AffectLocation.Damroll, 5)),
                [Sword] = Weapon(Sword, "sword", weight: 40, diceSides: 4),
                [Dagger] = Weapon(Dagger, "dagger", weight: 16, diceSides: 6),
                [Axe] = Weapon(Axe, "axe", weight: 64, diceSides: 8),
                [Wand] = Worn(Wand, "wand", new ObjectAffect(AffectLocation.Hitroll, 0)) with
                {
                    Type = "Wand", Weight = 12
                }
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
            Modifier = 5, // THAC0 15: 7 or more hits
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

    /// <summary>
    /// Legacy str_app: 19 adds 7 to damage, and 18/xx counts its bonus (on a damroll of 5). Strength stops at the
    /// race's maximum (legacy stat_check, #88): 18 for a human, 20 for a troll.
    /// </summary>
    [Theory]
    [InlineData("human", 10, 0, 5)]
    [InlineData("human", 18, 0, 7)]
    [InlineData("human", 18, 60, 8)]
    [InlineData("human", 19, 0, 7)] // counts as 18
    [InlineData("troll", 19, 0, 12)]
    public void Strength_AddsDamage(string race, int strength, int strengthAdd, int damage)
    {
        _attacker.Race = race;
        _attacker.Damroll = 5;
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

    /// <summary>
    /// The dice a mob's hit rolls are its attack dice, in order: nothing else is rolled first (the fallback for a mob
    /// without attack data is only rolled when used).
    /// </summary>
    [Fact]
    public void AMobsHit_RollsOnlyItsAttackDice()
    {
        var roll = Roll(new SequenceRandom(1, 2, 4));

        Assert.Equal(1 + 2 + 1, roll.Damage(_mob, _victim)); // 2d4 rolling 1 and 2, + 1
    }

    /// <summary>A mob's strength counts with its affects and gear (legacy GET_STR).</summary>
    [Fact]
    public void AMobsStrengthAffect_AddsDamage()
    {
        _mob.AddAffect(new Affect
        {
            Type = AffectType.Bless,
            Location = AffectLocation.Strength,
            Modifier = 9, // 19: +7
            DurationHours = 10
        });

        Assert.Equal(2 + 1 + 7, Roll(1).Damage(_mob, _victim));
    }

    /// <summary>
    /// Legacy dam *= (3 + POS_FIGHTING - pos) / 3 in integer math, as the C code computes it (its comments say
    /// x1.33 and x1.66 for sitting and resting), on a damroll of 9.
    /// </summary>
    [Theory]
    [InlineData(Position.Standing, 9)]
    [InlineData(Position.Sitting, 9)] // 4 / 3 = 1
    [InlineData(Position.Resting, 9)] // 5 / 3 = 1
    [InlineData(Position.Sleeping, 18)] // 6 / 3 = 2
    [InlineData(Position.Stunned, 18)] // 7 / 3 = 2
    public void AVictimNotOnTheirFeet_TakesMoreDamage(Position position, int damage)
    {
        _attacker.Damroll = 9;
        _mob.Position = position;

        Assert.Equal(damage, Roll(1).Damage(_attacker, _mob));
    }

    /// <summary>
    /// A melee hit that lands does at least 1 (legacy MAX(1, dam)), e.g. a weak attacker's bare hands.
    /// </summary>
    [Fact]
    public void AWeakHit_DoesAtLeastOne()
    {
        _attacker.Strength = 3; // -1, and bare hands roll 0

        Assert.Equal(1, Roll(1).Damage(_attacker, _mob));
    }

    /// <summary>Legacy get_thaco(): 20 − level / 3 for fighters, 20 − level / 4 for the rest (class content).</summary>
    [Theory]
    [InlineData("warrior", 30, 10)]
    [InlineData("magic-user", 30, 13)]
    public void APlayersThac0_ComesFromTheirClass(string characterClass, int level, int thac0)
    {
        var attacker = new PlayerState(4, "Veteran", Room, (byte)level, characterClass, "human", sex: 1)
        {
            Strength = 13, Intelligence = 13, Wisdom = 13, Movement = 100
        };

        // A roll of thac0 − 8 is the lowest that hits the victim at 8
        Assert.True(Roll(thac0 - 8).Hits(attacker, _victim));
        Assert.False(Roll(thac0 - 9).Hits(attacker, _victim));
    }

    /// <summary>
    /// A mob attacks with THAC0 20, less its to-hit bonus; a mob victim defends with its AC and dexterity.
    /// </summary>
    [Fact]
    public void MobsRollToHit_AndAreRolledAgainst()
    {
        // The rat: AC 10, dexterity 10, so (10 − 16) / 10 = 0; strength, int and wis 10 give −1 to hit (int and wis)
        Assert.True(Roll(20).Hits(_mob, _victim));
        Assert.False(Roll(11).Hits(_mob, _victim)); // THAC0 20 + 2 − 11 = 11 > 8
        Assert.True(Roll(20).Hits(_attacker, _mob));
        Assert.False(Roll(19).Hits(_attacker, _mob)); // 20 − 19 = 1 > 0
    }

    /// <summary>
    /// A victim who isn't awake is always hit, even on a natural 1 (legacy AWAKE(victim) in the miss test).
    /// </summary>
    [Fact]
    public void ASleepingVictim_IsAlwaysHit()
    {
        _victim.Position = Position.Sleeping;

        Assert.True(Roll(1).Hits(_attacker, _victim));
    }

    [Fact]
    public void ANaturalOne_MissesAnAwakeVictim()
    {
        _victim.ArmorClass = 1000;

        Assert.False(Roll(1).Hits(_attacker, _victim));
    }

    /// <summary>A victim out of movement is easier to hit: AC + 50 (here 13 instead of 8, so 7 hits).</summary>
    [Fact]
    public void AVictimOutOfMovement_IsEasierToHit()
    {
        Assert.False(Roll(7).Hits(_attacker, _victim));

        _victim.Movement = 0;

        Assert.True(Roll(7).Hits(_attacker, _victim));
    }

    /// <summary>Swinging a weapon costs its weight / 8 in movement, hit or miss; bare hands cost nothing.</summary>
    [Fact]
    public void SwingingAWeapon_CostsMovement()
    {
        Roll(1).Hits(_attacker, _victim);
        Assert.Equal(100, _attacker.Movement);

        Wear(_attacker, Sword, EquipmentSlot.Wield);
        Roll(1).Hits(_attacker, _victim);

        Assert.Equal(100 - 40 / 8, _attacker.Movement);
    }

    /// <summary>
    /// Out of movement, a player doesn't attack at all (legacy hit(), fight.c:1162); one time in four they pant for
    /// breath. With movement, nothing stops them.
    /// </summary>
    [Fact]
    public void APlayerOutOfMovement_DoesntAttack_AndSometimesPants()
    {
        Assert.Null(Roll(1).Winded(_attacker));

        _attacker.Movement = 0;

        Assert.Equal(
            new[] { "A:You pant for breath...", "O:$n pants for breath." },
            Roll(1).Winded(_attacker)!.Select(m => $"{m.Target.ToString()[0]}:{m.Template}"));
        Assert.Empty(Roll(2).Winded(_attacker)!);
        Assert.Null(Roll(1).Winded(_mob)); // mobs have no movement
    }

    /// <summary>A mob's dexterity counts with its affects and gear when it defends (legacy GET_DEX).</summary>
    [Fact]
    public void AMobsDexterityAffect_ChangesHowEasilyItIsHit()
    {
        Assert.False(Roll(19).Hits(_attacker, _mob)); // AC (10 − 16) / 10 = 0: needs 20
        _mob.AddAffect(new Affect
        {
            Type = AffectType.Bless,
            Location = AffectLocation.Dexterity,
            Modifier = -10, // dexterity 0: AC 10 / 10 = 1, so 19 hits
            DurationHours = 10
        });

        Assert.True(Roll(19).Hits(_attacker, _mob));
    }

    /// <summary>
    /// Only a weapon costs movement to swing (legacy ITEM_WEAPON or ITEM_FIREWEAPON): not a wielded wand.
    /// </summary>
    [Fact]
    public void AWieldedNonWeapon_CostsNoMovement_AndHitsBareHanded()
    {
        Wear(_attacker, Wand, EquipmentSlot.Wield);

        Roll(1).Hits(_attacker, _victim);

        Assert.Equal(100, _attacker.Movement);
        Assert.Equal(1, Roll(1).Damage(_attacker, _victim)); // bare hands: 0, at least 1
    }

    /// <summary>A two-handed weapon is swung like any wielded one.</summary>
    [Fact]
    public void ATwoHandedWeapon_CostsMovement_AndRollsItsDice()
    {
        Wear(_attacker, Axe, EquipmentSlot.BothHands);

        Roll(8).Hits(_attacker, _victim);

        Assert.Equal(100 - 64 / 8, _attacker.Movement);
        Assert.Equal(8, Roll(8).Damage(_attacker, _victim));
    }

    /// <summary>
    /// The dual-wield attack swings the held weapon (legacy hit(), fight.c:1244), for movement and dice.
    /// </summary>
    [Fact]
    public void TheDualAttack_SwingsTheHeldWeapon()
    {
        Wear(_attacker, Sword, EquipmentSlot.Wield);
        Wear(_attacker, Dagger, EquipmentSlot.Hold);

        Roll(6).Hits(_attacker, _victim, offHand: true);

        Assert.Equal(100 - 16 / 8, _attacker.Movement);
        Assert.Equal(6, Roll(6).Damage(_attacker, _victim, offHand: true)); // the dagger's 1d6, not the sword's 1d4
        Assert.Equal(4, Roll(6).Damage(_attacker, _victim));
    }

    /// <summary>Holding something that isn't a weapon, the dual attack swings the wielded weapon.</summary>
    [Fact]
    public void TheDualAttack_WithANonWeaponHeld_SwingsTheWieldedWeapon()
    {
        Wear(_attacker, Sword, EquipmentSlot.Wield);
        Wear(_attacker, Wand, EquipmentSlot.Hold);

        Roll(1).Hits(_attacker, _victim, offHand: true);

        Assert.Equal(100 - 40 / 8, _attacker.Movement);
    }

    /// <summary>
    /// A mob's stats stay within 0–25 with affects and gear (legacy stat_check); content can be far outside.
    /// </summary>
    [Theory]
    [InlineData(67108864, 0, 25)]
    [InlineData(10, -30, 0)]
    [InlineData(10, 5, 15)]
    public void AMobsStats_StayWithinLegacysRange(int intelligence, int modifier, int effective)
    {
        var stats = _mob.Definition.Stats with { Intelligence = intelligence };
        var mob = new MobInstance(9, _mob.Definition with { Stats = stats });
        mob.AddAffect(new Affect
        {
            Type = AffectType.Bless,
            Location = AffectLocation.Intelligence,
            Modifier = modifier,
            DurationHours = 10
        });

        Assert.Equal(effective, new EffectiveStats(_world, Rules).Get(mob, AffectLocation.Intelligence));
    }

    private MeleeRoll Roll(int dice) => Roll(new FixedRandom(dice));

    private MeleeRoll Roll(Random dice)
    {
        var skills = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        var formulas = new FormulaEvaluator(new Random(1));
        return new MeleeRoll(
            new CombatCalculator(new DodgeSkill(skills, formulas), new ParrySkill(skills, formulas), dice),
            _world,
            new EffectiveStats(_world, Rules));
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

    /// <summary>A weapon rolling 1d<paramref name="diceSides"/>.</summary>
    private static ObjectDefinition Weapon(int id, string name, int weight, int diceSides) =>
        new(id, name, name, $"A {name} lies here.", "Desc", "Weapon",
            new List<string>(), new List<string>(),
            new ObjectDetails { Weapon = new ObjectWeapon(1, diceSides, 0, 0) },
            new List<int>(), weight, 100, new List<ObjectAffect>());

    private static ObjectDefinition Worn(int id, string name, ObjectAffect affect) =>
        new(id, name, name, $"A {name} lies here.", "Desc", "Worn",
            new List<string>(), new List<string>(), null, new List<int>(), 1, 100, new List<ObjectAffect> { affect });
}

/// <summary>Dice that roll the given values in order, then repeat the last one.</summary>
internal sealed class SequenceRandom : Random
{
    private readonly Queue<int> _values;
    private int _last;

    public SequenceRandom(params int[] values)
    {
        _values = new Queue<int>(values);
    }

    public override int Next(int minValue, int maxValue) => Math.Clamp(Take(), minValue, maxValue - 1);

    public override int Next(int maxValue) => Math.Clamp(Take(), 0, maxValue - 1);

    private int Take() => _last = _values.Count > 0 ? _values.Dequeue() : _last;
}

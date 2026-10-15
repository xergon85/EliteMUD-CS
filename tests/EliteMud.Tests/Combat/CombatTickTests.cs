using EliteMud.Application.Ai;
using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Tests.Ai;
using EliteMud.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Tests.Combat;

/// <summary>
/// One combat round of the tick (legacy perform_violence()), as each player in the room sees it (#42 step 6). Every
/// hit is resolved as a melee hit, then dealt through the shared damage path, as legacy hit() then damage().
/// Fixed dice of 20: a player's to-hit roll against a player is a natural 20 (always hits), bare hands do
/// damroll + 2, and a mob's attack dice roll their maximum.
/// act() capitalizes a line's first letter, but the victim's hit line starts with a color code, so "a rat hits you"
/// stays lower case, as in legacy (dam_message wraps it in "§1r" before act()).
/// </summary>
public sealed class CombatTickTests : IDisposable
{
    private const int Room = 3002; // not the temple, so a respawn shows
    private const int Escape = 3003;
    private const int Ring = 200;
    private const int Sword = 201;
    private const int Dagger = 202;

    /// <summary>+50 max hit points while worn.</summary>
    private static readonly ObjectDefinition RingOfLife = new(
        Ring, "ring", "a ring of life", "A ring lies here.", "Desc", "Worn",
        new List<string>(), new List<string>(), null, new List<int>(), 1, 100,
        new List<ObjectAffect> { new(AffectLocation.MaxHit, 50) });
    private const int Temple = CharacterStart.StartRoom;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly WorldState _world;
    private readonly MobInstance _mob;
    private readonly MobInstance _secondMob;
    private readonly TestConnection _fighter;
    private readonly TestConnection _victim;
    private readonly TestConnection _observer;
    private readonly GameTickService _tick;
    private readonly SettableRandom _dice = new(20);

    public CombatTickTests()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator(new Random(1));
        var skills = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        var calculator = new CombatCalculator(
            new DodgeSkill(skills, formulas), new ParrySkill(skills, formulas), _dice);
        var experienceService = new ExperienceService(rules, formulas, new Random(1));
        var progression = new PlayerProgression(experienceService, rules);

        // Level 5, alignment -1000, 400 XP (a 100 kill share); one attack of 2d4 + 1 (9 with fixed dice)
        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 100, -1000,
            new List<MobAttack> { new("bite", 0, 100, 2, 4, 1) }, null,
            Experience: 400);
        _mob = new MobInstance(1, mobDefinition);
        _secondMob = new MobInstance(2, mobDefinition); // in the arena too, fighting only when a test says so

        // Every way out of the arena leads to the same room, so a flee never depends on the direction rolled
        var exits = Enum.GetValues<Direction>().Select(direction => new ExitDefinition(direction, Escape)).ToList();
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                [Room] = new(Room, "Arena", "Desc", exits),
                [Escape] = new(Escape, "Alley", "A narrow alley.", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = mobDefinition },
            new Dictionary<int, ObjectDefinition>
            {
                [Ring] = RingOfLife,
                [Sword] = Weapon(Sword, "a sword", weight: 40),
                [Dagger] = Weapon(Dagger, "a dagger", weight: 16)
            },
            new Dictionary<int, List<MobInstance>>
            {
                [Temple] = new(), [Room] = new() { _mob, _secondMob }, [Escape] = new()
            },
            new Dictionary<int, List<ObjectInstance>> { [Temple] = new(), [Room] = new(), [Escape] = new() },
            new List<ZoneDefinition>());

        _fighter = new TestConnection(1, new PlayerState(1, "Fighter", Room, level: 10, "warrior", "human", sex: 1)
        {
            HitPoints = 100,
            MaxHitPoints = 100,
            Movement = 100,
            Damroll = 8, // 11 a hit with fixed dice (+1 strength bonus)
            Position = Position.Standing
        });
        _victim = new TestConnection(2, new PlayerState(2, "Victim", Room, level: 5, "warrior", "human", sex: 1)
        {
            HitPoints = 100,
            MaxHitPoints = 100,
            Movement = 100,
            Experience = 5000,
            Position = Position.Standing
        });
        _observer = new TestConnection(3, new PlayerState(3, "Observer", Room, level: 5, "warrior", "human", sex: 1));

        var connections = new ConnectionRegistry();
        connections.SetProvider(() => new[] { _fighter.Context, _victim.Context, _observer.Context });
        foreach (var connection in connections.GetConnections())
        {
            _world.Population.Enter(connection.Player, connection.Player.RoomId);
        }

        var services = new ServiceCollection().BuildServiceProvider();
        var gate = new GameStateGate();
        var stats = new EffectiveStats(rules);
        var look = new LookCommandHandler(_world, new MockScriptEngine(), RulesContent.Empty);
        var damage = new CombatDamage(
            calculator,
            new CombatExperience(progression, experienceService),
            _world,
            new ICombatListener[] { new MobMemory() },
            new FleeHandler(_world, progression),
            stats);
        _tick = new GameTickService(
            connections,
            services,
            _world,
            damage,
            new MeleeRoll(calculator, stats),
            stats,
            new SkillMessageSender(new ActMessageService(), connections, look),
            calculator,
            new CharacterSaveQueue(services, gate),
            new MobAiService(new MockScriptEngine(), new PathfindingService()),
            gate);
    }

    public void Dispose()
    {
        _fighter.Dispose();
        _victim.Dispose();
        _observer.Dispose();
    }

    private PlayerState Fighter => _fighter.Context.Player;
    private PlayerState Victim => _victim.Context.Player;

    /// <summary>The mob hits back once a round, on its own turn (legacy perform_violence; decision 10).</summary>
    [Fact]
    public async Task PlayerHittingAMob_TheMobHitsBackOnItsOwnTurn()
    {
        Fight(Fighter, _mob);
        Fight(_mob, Fighter);

        await Round();

        Assert.Equal(89, _mob.HitPoints);
        Assert.Equal(91, Fighter.HitPoints);
        Assert.Equal(
            new[] { "You hit a rat extremely hard.", "a rat hits you extremely hard. [91/100 HP]" },
            _fighter.Lines());
        Assert.Equal(
            new[] { "Fighter hits a rat extremely hard.", "A rat hits Fighter extremely hard." },
            _observer.Lines());
    }

    /// <summary>
    /// A mob joins a fight in whatever position it was in (legacy set_fighting) and spends its first turn getting
    /// into the fighting position (legacy perform_violence, fight.c:1690). From standing that's silent.
    /// </summary>
    [Fact]
    public async Task AMobJustDrawnIntoAFight_SpendsItsTurnGettingReady()
    {
        Fight(Fighter, _mob);
        _mob.Fighting = Fighter;
        _mob.Position = Position.Standing;

        await Round();

        Assert.Equal(Position.Fighting, _mob.Position);
        Assert.Equal(100, Fighter.HitPoints);
        Assert.Equal(new[] { "You hit a rat extremely hard." }, _fighter.Lines());
    }

    /// <summary>A mob knocked down (e.g. bashed) or asleep scrambles up instead of attacking.</summary>
    [Fact]
    public async Task AKnockedDownMob_ScramblesToItsFeet_InsteadOfAttacking()
    {
        Fight(Fighter, _mob);
        _mob.Fighting = Fighter;
        _mob.Position = Position.Sitting;

        await Round();

        Assert.Equal(Position.Fighting, _mob.Position);
        Assert.Equal(100, Fighter.HitPoints);
        Assert.Equal(
            new[] { "You hit a rat extremely hard.", "A rat scrambles to its feet!" }, // sitting: no extra damage
            _fighter.Lines());
        Assert.Equal(
            new[] { "Fighter hits a rat extremely hard.", "A rat scrambles to its feet!" },
            _observer.Lines());
    }

    /// <summary>
    /// A player sitting in a fight (e.g. after a failed bash) gets up instead of their first attack (legacy
    /// perform_violence, fight.c:1701).
    /// </summary>
    [Fact]
    public async Task ASittingPlayer_ScramblesToTheirFeet_InsteadOfTheirFirstAttack()
    {
        Fight(Fighter, _mob);
        Fighter.Position = Position.Sitting;

        await Round();

        Assert.Equal(Position.Fighting, Fighter.Position);
        Assert.Equal(100, _mob.HitPoints);
        Assert.Equal(new[] { "You regain your senses and scramble to your feet." }, _fighter.Lines());
        Assert.Equal(new[] { "Fighter scrambles to his feet!" }, _observer.Lines());
    }

    /// <summary>Both sides roll to hit (#48): on a natural 1 the player and the mob both miss.</summary>
    [Fact]
    public async Task APlayerAndAMob_BothMissOnANaturalOne()
    {
        Fight(Fighter, _mob);
        Fight(_mob, Fighter);
        _dice.Value = 1;

        await Round();

        Assert.Equal(100, _mob.HitPoints);
        Assert.Equal(100, Fighter.HitPoints);
        Assert.Equal(
            new[] { "You miss a rat with your hit.", "a rat misses you with their hit. [100/100 HP]" },
            _fighter.Lines());
        Assert.Equal(
            new[] { "Fighter misses a rat with their hit.", "A rat misses Fighter with their hit." },
            _observer.Lines());
    }

    /// <summary>
    /// A player out of movement doesn't attack (legacy hit(), fight.c:1162); on a 1 in 4 they pant for breath.
    /// </summary>
    [Fact]
    public async Task APlayerOutOfMovement_PantsInsteadOfAttacking()
    {
        Fight(Fighter, _mob);
        Fighter.Movement = 0;
        _dice.Value = 1;

        await Round();

        Assert.Equal(100, _mob.HitPoints);
        Assert.Equal(new[] { "You pant for breath..." }, _fighter.Lines());
        Assert.Equal(new[] { "Fighter pants for breath." }, _observer.Lines());
    }

    /// <summary>
    /// The dual-wield attack (the fifth) swings the held weapon: a round with the main hand and the off hand costs
    /// each weapon's weight / 8 (legacy hit(), fight.c:1244).
    /// </summary>
    [Fact]
    public async Task TheDualAttack_SwingsTheHeldWeapon()
    {
        Fight(Fighter, _mob);
        Fighter.SetSkill(SkillType.DualWield, 100); // the dice of 20 are under 100: the dual attack happens
        Fighter.Equip(_world.CreateObjectInstance(Sword), EquipmentSlot.Wield);
        Fighter.Equip(_world.CreateObjectInstance(Dagger), EquipmentSlot.Hold);

        await Round();

        Assert.Equal(100 - 40 / 8 - 16 / 8, Fighter.Movement);
    }

    [Fact]
    public async Task PlayerKillingAMob_LeavesACorpse_AndGivesKillExperience()
    {
        Fight(Fighter, _mob);
        Fight(_mob, Fighter);
        _mob.HitPoints = 5;

        await Round();

        Assert.DoesNotContain(_mob, _world.GetMobsInRoom(Room));
        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Null(Fighter.Fighting);
        Assert.Equal(62, Fighter.Alignment);
        Assert.Equal(
            new[] { "You hit a rat extremely hard.", "A rat is DEAD!!", "You receive 100 experience points." },
            _fighter.Lines());
        Assert.Equal(
            new[] { "Fighter hits a rat extremely hard.", "A rat is dead!  R.I.P." },
            _observer.Lines());
    }

    /// <summary>A player hit by a mob fights back (legacy set_fighting in damage()).</summary>
    [Fact]
    public async Task MobHittingAPlayer_TheyFightBack()
    {
        Fight(_mob, Victim);

        await Round();

        Assert.Equal(91, Victim.HitPoints);
        Assert.Same(_mob, Victim.Fighting);
        Assert.Equal(new[] { "a rat hits you extremely hard. [91/100 HP]" }, _victim.Lines());
        Assert.Equal(new[] { "A rat hits Victim extremely hard." }, _observer.Lines());
    }

    /// <summary>
    /// Once their target is dead, the next mob that hits a player becomes their target (legacy: damage() starts a
    /// fight for whoever has none).
    /// </summary>
    [Fact]
    public async Task AfterTheirTargetDies_APlayerFightsTheNextMobThatHitsThem()
    {
        Fight(Fighter, _mob);
        Fight(_mob, Fighter);
        _mob.HitPoints = 5;
        Fight(_secondMob, Fighter);

        await Round();

        Assert.DoesNotContain(_mob, _world.GetMobsInRoom(Room));
        Assert.Same(_secondMob, Fighter.Fighting);
        Assert.Equal(91, Fighter.HitPoints);
    }

    [Fact]
    public async Task MobKillingAPlayer_LeavesACorpse_AndRespawnsThemAtTheTemple()
    {
        Fight(_mob, Victim);
        Victim.HitPoints = -5; // the 9 of the bite kills

        await Round();

        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Equal(Temple, Victim.RoomId);
        Assert.Equal(Position.Standing, Victim.Position);
        Assert.Equal(100, Victim.HitPoints);
        Assert.Equal(5000 - 3333, Victim.Experience);
        Assert.Null(_mob.Fighting);
        Assert.Equal(
            new[]
            {
                "a rat hits you extremely hard. [-14/100 HP]",
                "You are dead!  Sorry...",
                "You lose 3333 experience points.",
                "You have been resurrected..."
            },
            _victim.Lines());
        Assert.Equal(
            new[] { "A rat hits Victim extremely hard.", "Victim is dead!  R.I.P." },
            _observer.Lines());
    }

    /// <summary>
    /// The killing hit is worded and shown against the max hit points the victim had when it landed, not after
    /// their gear went into the corpse.
    /// </summary>
    [Fact]
    public async Task MobKillingAPlayer_ShowsTheHitAgainstTheirMaxHitPointsWithGear()
    {
        var ring = _world.CreateObjectInstance(Ring)!;
        Victim.Equip(ring, EquipmentSlot.FingerRight);
        Fight(_mob, Victim);
        Victim.HitPoints = -5;

        await Round();

        Assert.Equal(Temple, Victim.RoomId);
        Assert.Equal("a rat hits you very hard. [-14/150 HP]", _victim.Lines()[0]);
    }

    /// <summary>The stunned fight on; only below stunned does the fight stop (legacy damage(), fight.c:1026).</summary>
    [Fact]
    public async Task MobStunningAPlayer_TellsThemAndTheRoom_AndBothFightOn()
    {
        Fight(_mob, Victim);
        Victim.Fighting = _mob; // fighting back, but resting, so this round is only about the mob's hit
        Victim.Position = Position.Resting;
        Victim.HitPoints = 8; // the 9 of the bite (no extra against a resting victim) leaves -1: stunned

        await Round();

        Assert.Equal(Position.Stunned, Victim.Position);
        Assert.Same(_mob, Victim.Fighting);
        Assert.Same(Victim, _mob.Fighting);
        Assert.Equal(
            new[]
            {
                "a rat hits you extremely hard. [-1/100 HP]",
                "You're stunned, but will probably regain consciousness again."
            },
            _victim.Lines());
        Assert.Equal(
            new[]
            {
                "A rat hits Victim extremely hard.",
                "Victim is stunned, but will probably regain consciousness again."
            },
            _observer.Lines());
    }

    [Fact]
    public async Task MobHittingAPlayerBelowTheirWimpy_MakesThemFlee()
    {
        Fight(_mob, Victim);
        Victim.WimpyLevel = 20;
        Victim.HitPoints = 25; // the 9 of the bite leaves 16

        await Round();

        Assert.Equal(Escape, Victim.RoomId);
        Assert.Null(Victim.Fighting);
        Assert.Null(_mob.Fighting);
        Assert.Equal(
            new[]
            {
                "a rat hits you extremely hard. [16/100 HP]",
                "You wish that your wounds would stop BLEEDING so much!",
                "You wimp out, and attempt to flee!",
                "You flee head over heels.",
                "Alley",
                "A narrow alley.",
                "[Exits: None!]"
            },
            _victim.Lines());
        Assert.Equal(
            new[] { "A rat hits Victim extremely hard.", "Victim panics, and attempts to flee." },
            _observer.Lines());
    }

    /// <summary>
    /// An opponent who left the room ends the fight on both sides, without a hit (legacy perform_violence).
    /// </summary>
    [Fact]
    public async Task AnOpponentWhoLeft_EndsTheFight()
    {
        Fight(Fighter, _mob);
        Fight(_mob, Fighter);
        _world.MoveMob(_mob.InstanceId, Room, Escape);

        await Round();

        Assert.Null(Fighter.Fighting);
        Assert.Null(_mob.Fighting);
        Assert.Equal(100, _mob.HitPoints);
        Assert.Equal(100, Fighter.HitPoints);
        Assert.Equal(new[] { "Your opponent has left." }, _fighter.Lines());
    }

    [Fact]
    public async Task PlayerHittingAPlayer_TheyFightBack()
    {
        Fight(Fighter, Victim);

        await Round();

        Assert.Equal(89, Victim.HitPoints);
        Assert.Same(Fighter, Victim.Fighting);
        Assert.Equal(new[] { "You hit Victim extremely hard." }, _fighter.Lines());
        Assert.Equal(new[] { "Fighter hits you extremely hard. [89/100 HP]" }, _victim.Lines());
        Assert.Equal(new[] { "Fighter hits Victim extremely hard." }, _observer.Lines());
    }

    [Fact]
    public async Task PlayerKillingAPlayer_LeavesACorpse_AndRespawnsThemAtTheTemple()
    {
        Fight(Fighter, Victim);
        Victim.HitPoints = -5; // the 11 of the hit kills

        await Round();

        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Equal(Temple, Victim.RoomId);
        Assert.Equal(5000 - 3333, Victim.Experience);
        Assert.Null(Fighter.Fighting);
        Assert.Equal(
            new[] { "You hit Victim extremely hard.", "Victim is DEAD!!", "You receive 1250 experience points." },
            _fighter.Lines());
        Assert.Equal(
            new[]
            {
                "Fighter hits you extremely hard. [-16/100 HP]",
                "You are dead!  Sorry...",
                "You lose 3333 experience points.",
                "You have been resurrected..."
            },
            _victim.Lines());
        Assert.Equal(
            new[] { "Fighter hits Victim extremely hard.", "Victim is dead!  R.I.P." },
            _observer.Lines());
    }

    /// <summary>The attacker fights the victim; the victim only fights back when the test says so.</summary>
    private static void Fight(ICombatant attacker, ICombatant victim)
    {
        attacker.Fighting = victim;
        attacker.Position = Position.Fighting;
    }

    private Task Round() => _tick.ProcessCombatRoundAsync(CancellationToken.None);

    private static ObjectDefinition Weapon(int id, string name, int weight) =>
        new(id, name, name, $"{name} lies here.", "Desc", "Weapon",
            new List<string>(), new List<string>(),
            new ObjectDetails { Weapon = new ObjectWeapon(1, 4, 0, 0) },
            new List<int>(), weight, 100, new List<ObjectAffect>());
}

/// <summary>Dice that roll one value, which a test can change.</summary>
internal sealed class SettableRandom : Random
{
    public SettableRandom(int value)
    {
        Value = value;
    }

    public int Value { get; set; }

    public override int Next(int minValue, int maxValue) => Math.Clamp(Value, minValue, maxValue - 1);

    public override int Next(int maxValue) => Math.Clamp(Value, 0, maxValue - 1);
}

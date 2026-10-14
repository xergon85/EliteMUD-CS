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
/// One combat round of the tick (legacy perform_violence()), as each player in the room sees it (#42 step 6).
/// These pin what the tick does today, before it moves onto the shared damage path; each later change shows up
/// as a deliberate change to the expected lines.
/// Fixed dice of 20: a player's to-hit roll is a natural 20 (always hits), bare hands do damroll + 2, and a mob's
/// attack dice roll their maximum. The mob's counterattack uses the shared random (level + 1d4), so its lines are
/// matched loosely.
/// </summary>
public sealed class CombatTickTests : IDisposable
{
    private const int Room = 3002; // not the temple, so a respawn shows
    private const int Escape = 3003;
    private const int Temple = CharacterStart.StartRoom;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly WorldState _world;
    private readonly MobInstance _mob;
    private readonly TestConnection _fighter;
    private readonly TestConnection _victim;
    private readonly TestConnection _observer;
    private readonly GameTickService _tick;

    public CombatTickTests()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator(new Random(1));
        var skills = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        var calculator = new CombatCalculator(
            new DodgeSkill(skills, formulas), new ParrySkill(skills, formulas), new FixedRandom(20));
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
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Temple] = new(), [Room] = new() { _mob }, [Escape] = new() },
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

        var services = new ServiceCollection().BuildServiceProvider();
        var gate = new GameStateGate();
        _tick = new GameTickService(
            connections,
            services,
            _world,
            new ActMessageService(),
            new FleeHandler(_world, calculator, progression),
            new LookCommandHandler(_world, new MockScriptEngine(), connections, RulesContent.Empty),
            calculator,
            new CharacterSaveQueue(services, _world, gate),
            new MobAiService(new MockScriptEngine(), new PathfindingService()),
            new CombatExperience(progression, experienceService),
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

    /// <summary>
    /// The mob hits back after every player attack (level + 1d4), then again on its own turn with its attack dice.
    /// Legacy: once a round, on its own turn (decision 10).
    /// </summary>
    [Fact]
    public async Task PlayerHittingAMob_TheMobHitsBackAfterTheHit_AndAgainOnItsOwnTurn()
    {
        Fight(Fighter, _mob);
        Fight(_mob, Fighter);

        await Round();

        Assert.Equal(89, _mob.HitPoints);
        var fighterLines = _fighter.Lines();
        Assert.Equal(3, fighterLines.Count);
        Assert.Equal("You hit a rat extremely hard.", fighterLines[0]);
        Assert.Matches(@"^a rat hits you (very|extremely) hard\. \[9[1-4]/100 HP\]$", fighterLines[1]); // counterattack
        Assert.Matches(@"^a rat hits you extremely hard\. \[8[2-5]/100 HP\]$", fighterLines[2]); // its own turn
        var observerLines = _observer.Lines();
        Assert.Equal(3, observerLines.Count);
        Assert.Equal("Fighter hits a rat extremely hard.", observerLines[0]);
        Assert.Matches(@"^a rat hits Fighter (very|extremely) hard\.$", observerLines[1]);
        Assert.Equal("a rat hits Fighter extremely hard.", observerLines[2]);
    }

    /// <summary>Not yet the shared death: "You have slain", an alignment line, and "is dead!" without R.I.P.</summary>
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
            new[]
            {
                "You hit a rat extremely hard.",
                "You have slain a rat! You feel more virtuous. (+62)",
                "You receive 100 experience points."
            },
            _fighter.Lines());
        Assert.Equal(
            new[]
            {
                "Fighter hits a rat extremely hard.",
                "a rat is dead!"
            },
            _observer.Lines());
    }

    [Fact]
    public async Task MobHittingAPlayer()
    {
        Fight(_mob, Victim);

        await Round();

        Assert.Equal(91, Victim.HitPoints);
        Assert.Equal(
            new[]
            {
                "a rat hits you extremely hard. [91/100 HP]"
            },
            _victim.Lines());
        Assert.Equal(
            new[]
            {
                "a rat hits Victim extremely hard."
            },
            _observer.Lines());
    }

    /// <summary>Not yet the shared death: the room line has one space after "dead!" where legacy has two.</summary>
    [Fact]
    public async Task MobKillingAPlayer_LeavesACorpse_AndRespawnsThemAtTheTemple()
    {
        Fight(_mob, Victim);
        Victim.HitPoints = -5; // incapacitated: the 9 of the bite kills

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
            new[]
            {
                "a rat hits Victim extremely hard.",
                "Victim is dead! R.I.P."
            },
            _observer.Lines());
    }

    /// <summary>
    /// "You are stunned" where legacy says "You're stunned", and nothing to the room. Legacy also keeps a stunned
    /// victim fighting and only stops the fight below stunned.
    /// </summary>
    [Fact]
    public async Task MobStunningAPlayer_StopsThemFighting_TheMobFightsOn()
    {
        Fight(_mob, Victim);
        Victim.Fighting = _mob; // fighting back, but this test is about the mob's hit, so not standing
        Victim.Position = Position.Sitting;
        Victim.HitPoints = 11; // the bite does 12 against a sitting victim (9 × 4/3), leaving -1: stunned

        await Round();

        Assert.Equal(Position.Stunned, Victim.Position);
        Assert.Null(Victim.Fighting);
        Assert.Same(Victim, _mob.Fighting);
        Assert.Equal(
            new[]
            {
                "a rat hits you extremely hard. [-1/100 HP]",
                "You are stunned, but will probably regain consciousness again."
            },
            _victim.Lines());
        Assert.Equal(
            new[]
            {
                "a rat hits Victim extremely hard."
            },
            _observer.Lines());
    }

    /// <summary>No "panics, and attempts to flee" line to the room, which legacy do_flee sends.</summary>
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
            new[]
            {
                "a rat hits Victim extremely hard."
            },
            _observer.Lines());
    }

    [Fact]
    public async Task PlayerHittingAPlayer()
    {
        Fight(Fighter, Victim);

        await Round();

        Assert.Equal(89, Victim.HitPoints);
        Assert.Equal(
            new[]
            {
                "You hit Victim extremely hard."
            },
            _fighter.Lines());
        Assert.Equal(
            new[]
            {
                "Fighter hits you extremely hard. [89/100 HP]"
            },
            _victim.Lines());
        Assert.Equal(
            new[]
            {
                "Fighter hits Victim extremely hard."
            },
            _observer.Lines());
    }

    /// <summary>
    /// Not yet the shared death: "You have slain", "Your soul darkens" and no "is DEAD!!"; the kill experience comes
    /// before the alignment line.
    /// </summary>
    [Fact]
    public async Task PlayerKillingAPlayer_LeavesACorpse_AndRespawnsThemAtTheTemple()
    {
        Fight(Fighter, Victim);
        Victim.HitPoints = -5; // incapacitated: the 11 of the hit kills

        await Round();

        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Equal(Temple, Victim.RoomId);
        Assert.Equal(5000 - 3333, Victim.Experience);
        Assert.Null(Fighter.Fighting);
        Assert.Equal(
            new[]
            {
                "You hit Victim extremely hard.",
                "You have slain Victim!",
                "You receive 1250 experience points.",
                "Your soul darkens from the murder. (0)"
            },
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
            new[]
            {
                "Fighter hits Victim extremely hard.",
                "Victim is dead! R.I.P."
            },
            _observer.Lines());
    }

    /// <summary>The attacker fights the victim; the victim only fights back when the test says so.</summary>
    private static void Fight(ICombatant attacker, ICombatant victim)
    {
        attacker.Fighting = victim;
        attacker.Position = Position.Fighting;
    }

    private Task Round() => _tick.ProcessCombatRoundAsync(CancellationToken.None);
}

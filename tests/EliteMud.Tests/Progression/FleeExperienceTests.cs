using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Progression;

/// <summary>Fleeing loses experience through the normal rules (legacy act.offensive.c → gain_exp).</summary>
public class FleeExperienceTests
{
    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly FleeHandler _flee;
    private readonly WorldState _world;
    private readonly List<MobInstance> _mobsInRoomOne = new();

    public FleeExperienceTests()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator();
        var metadata = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                // Exits every way, so a flee always gets out
                [1] = new(
                    1, "From", "Desc", Enum.GetValues<Direction>().Select(d => new ExitDefinition(d, 2)).ToList()),
                [2] = new(2, "To", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [1] = _mobsInRoomOne, [2] = new() },
            new Dictionary<int, List<ObjectInstance>> { [1] = new(), [2] = new() },
            new List<ZoneDefinition>());
        _flee = new FleeHandler(
            _world,
            new CombatCalculator(new DodgeSkill(metadata, formulas), new ParrySkill(metadata, formulas)),
            new PlayerProgression(new ExperienceService(rules, formulas), rules));
    }

    /// <summary>
    /// Fleeing costs damage done × the opponent's level, but only for an opponent still in the room: one who left
    /// costs nothing (legacy fights always end when the two part).
    /// </summary>
    [Theory]
    [InlineData(1, 300)] // opponent here: 30 damage × level 10
    [InlineData(2, 0)]   // opponent already gone
    public void FleeExperienceLoss_CountsOnlyAnOpponentStillHere(int opponentRoom, int expectedLoss)
    {
        var player = Player(level: 10, experience: 5000);
        var opponent = new PlayerState(2, "Other", opponentRoom, level: 10, "warrior", "human", sex: 1);
        opponent.MaxHitPoints = 100;
        opponent.HitPoints = 70;
        player.Fighting = opponent;

        var result = _flee.AttemptFlee(player, 1, () => new[] { opponent }, () => _mobsInRoomOne);

        Assert.True(result.Success);
        Assert.Equal(expectedLoss, result.ExperienceLoss);
    }

    /// <summary>Whoever was fighting the player in the room they fled from stops; other fights go on.</summary>
    [Fact]
    public void Flee_StopsTheMobsFightingThePlayer_AndNoOtherFights()
    {
        var player = Player(level: 10, experience: 5000);
        var someoneElse = new PlayerState(2, "Other", 1, level: 10, "warrior", "human", sex: 1);
        var definition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);
        var fightingUs = new MobInstance(1, definition);
        var fightingThem = new MobInstance(2, definition);
        _mobsInRoomOne.AddRange(new[] { fightingUs, fightingThem });
        fightingUs.Fighting = player;
        fightingUs.Position = Position.Fighting;
        fightingThem.Fighting = someoneElse;
        player.Fighting = fightingUs;

        _flee.ApplyFleeResult(player, new FleeResult(true, 2, 0, 1), () => new[] { someoneElse });

        Assert.Null(player.Fighting);
        Assert.Null(fightingUs.Fighting);
        Assert.Equal(Position.Standing, fightingUs.Position);
        Assert.Same(someoneElse, fightingThem.Fighting);
    }

    [Fact]
    public void Flee_LosesExperienceWithTheLegacyMessage()
    {
        var player = Player(level: 10, experience: 5000);

        var messages = _flee.ApplyFleeResult(player, new FleeResult(true, 2, 1200, 1), Array.Empty<PlayerState>);

        Assert.Equal(3800, player.Experience);
        Assert.Equal("You lose 1200 experience points.", Assert.Single(messages));
        Assert.Equal(2, player.RoomId);
    }

    [Fact]
    public void Flee_NeverTakesExperienceBelowZero()
    {
        var player = Player(level: 10, experience: 300);

        var messages = _flee.ApplyFleeResult(player, new FleeResult(true, 2, 1200, 1), Array.Empty<PlayerState>);

        Assert.Equal(0, player.Experience);
        Assert.Equal("You lose 300 experience points.", Assert.Single(messages));
    }

    [Fact]
    public void Immortals_LoseNothing()
    {
        var player = Player(level: 110, experience: 5000);

        var messages = _flee.ApplyFleeResult(player, new FleeResult(true, 2, 1200, 1), Array.Empty<PlayerState>);

        Assert.Equal(5000, player.Experience);
        Assert.Empty(messages);
    }

    private static PlayerState Player(int level, int experience) =>
        new(1, "Tester", 1, (byte)level, "warrior", "human", sex: 1) { Experience = experience };
}

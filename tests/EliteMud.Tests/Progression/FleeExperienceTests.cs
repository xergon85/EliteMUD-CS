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

    public FleeExperienceTests()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator();
        var metadata = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [1] = new(1, "From", "Desc", new List<ExitDefinition>()),
                [2] = new(2, "To", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [1] = new(), [2] = new() },
            new Dictionary<int, List<ObjectInstance>> { [1] = new(), [2] = new() },
            new List<ZoneDefinition>());
        _flee = new FleeHandler(
            world,
            new CombatCalculator(new DodgeSkill(metadata, formulas), new ParrySkill(metadata, formulas)),
            new PlayerProgression(new ExperienceService(rules, formulas), rules));
    }

    [Fact]
    public void Flee_LosesExperienceWithTheLegacyMessage()
    {
        var player = Player(level: 10, experience: 5000);

        var messages = _flee.ApplyFleeResult(player, new FleeResult(true, 2, 1200, 1), Array.Empty<PlayerState>, 1);

        Assert.Equal(3800, player.Experience);
        Assert.Equal("You lose 1200 experience points.", Assert.Single(messages));
        Assert.Equal(2, player.RoomId);
    }

    [Fact]
    public void Flee_NeverTakesExperienceBelowZero()
    {
        var player = Player(level: 10, experience: 300);

        var messages = _flee.ApplyFleeResult(player, new FleeResult(true, 2, 1200, 1), Array.Empty<PlayerState>, 1);

        Assert.Equal(0, player.Experience);
        Assert.Equal("You lose 300 experience points.", Assert.Single(messages));
    }

    [Fact]
    public void Immortals_LoseNothing()
    {
        var player = Player(level: 110, experience: 5000);

        var messages = _flee.ApplyFleeResult(player, new FleeResult(true, 2, 1200, 1), Array.Empty<PlayerState>, 1);

        Assert.Equal(5000, player.Experience);
        Assert.Empty(messages);
    }

    private static PlayerState Player(int level, int experience) =>
        new(1, "Tester", 1, (byte)level, "warrior", "human", sex: 1) { Experience = experience };
}

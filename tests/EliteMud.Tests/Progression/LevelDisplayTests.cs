using EliteMud.Application.Commands.Look;
using EliteMud.Application.Commands.Score;
using EliteMud.Application.Progression;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Progression;

/// <summary>Level, XP and titles as the player sees them: score, look, the title fallback and setlevel.</summary>
public class LevelDisplayTests
{
    private static readonly RulesContent Rules =
        RulesContentLoader.Load(Path.Combine(RealWorld.RepositoryRoot, "content")).Rules;

    private static readonly WorldState World = new(
        new WorldDefinition(new Dictionary<int, RoomDefinition> { [3001] = new(3001, "Temple", "Desc", new List<ExitDefinition>()) }),
        new Dictionary<int, MobDefinition>(),
        new Dictionary<int, ObjectDefinition>(),
        new Dictionary<int, List<MobInstance>> { [3001] = new() },
        new Dictionary<int, List<ObjectInstance>> { [3001] = new() },
        new List<ZoneDefinition>());

    private static PlayerProgression Progression()
    {
        var random = new Random(1);
        return new PlayerProgression(new ExperienceService(Rules, new FormulaEvaluator(random), random), Rules);
    }

    private static PlayerState Warrior(int level = 1, int id = 1, string name = "Tester") =>
        new(id, name, 3001, (byte)level, "warrior", "human", sex: 1)
        {
            Constitution = 16,
            MaxHitPoints = 20,
            MaxMana = 100,
            MaxMovement = 100
        };

    [Fact]
    public void Score_ShowsTheRealXpToTheNextLevel()
    {
        var player = Warrior();
        player.Experience = 100;

        var score = new ScoreHandler(Rules).Handle(player, World).Message;

        Assert.Contains("You need #R2566#N exp to reach your next level.", score); // 2666 - 100
        Assert.Contains("the #wHuman#N Swordpupil", score); // no stored title yet: the class title
    }

    [Theory]
    [InlineData(109, true)]  // legacy shows it below LEVEL_DEITY
    [InlineData(110, false)]
    public void Score_ShowsXpToNextLevelForMortalsOnly(int level, bool shown)
    {
        var score = new ScoreHandler(Rules).Handle(Warrior(level), World).Message;

        Assert.Equal(shown, score.Contains("to reach your next level"));
    }

    [Fact]
    public void DisplayTitle_PrefersTheStoredTitle()
    {
        var player = Warrior();
        player.Title = "the Mighty";

        Assert.Equal("the Mighty", PlayerProgression.DisplayTitle(player, Rules));
    }

    [Fact]
    public void DisplayTitle_IsEmptyForUnknownClass()
    {
        var player = Warrior();
        player.CharacterClass = "necromancer";

        Assert.Equal("", PlayerProgression.DisplayTitle(player, Rules));
    }

    [Fact]
    public void Look_ShowsOtherPlayersWithTheirTitle()
    {
        var other = Warrior(level: 3, id: 2, name: "Xergon");
        var view = new LookHandler(World, Rules, () => new[] { other }).Handle(Warrior());

        Assert.Equal("#CXergon#N the #wHuman#N Recruit is standing here.", Assert.Single(view.PlayerLines));
    }

    [Fact]
    public void SetLevel_Up_RunsEveryLevelUp()
    {
        var player = Warrior();
        player.Experience = 500;

        var result = Progression().SetLevel(player, 30);

        Assert.Equal(30, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.True(player.SaveRequested);
        Assert.Equal("the #wHuman#N Man-at-arms", player.Title); // row 30/3 + 1 = 11
        Assert.InRange(result.HitPointGain, 29 * 9, 29 * 14); // 1d5 + con/2 for 29 levels, con 16-18
        Assert.Equal(20 + result.HitPointGain, player.MaxHitPoints);
        Assert.Equal(29 * 3, result.MovementGain);
    }

    [Fact]
    public void SetLevel_Down_OnlyChangesLevelAndTitle()
    {
        var player = Warrior(level: 30);

        var result = Progression().SetLevel(player, 5);

        Assert.Equal(5, player.Level);
        Assert.Equal(20, player.MaxHitPoints);
        Assert.Equal(0, result.HitPointGain);
        Assert.Equal(ExperienceService.Title(Rules.Classes["warrior"], Rules.Races["human"], Sex.Male, 5), player.Title);
    }
}

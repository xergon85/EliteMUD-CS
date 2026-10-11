using EliteMud.Application.Progression;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Progression;

public class PlayerProgressionTests
{
    private static readonly RulesContent Rules =
        RulesContentLoader.Load(Path.Combine(RealWorld.RepositoryRoot, "content")).Rules;

    private static PlayerProgression Progression(int seed = 1)
    {
        var random = new Random(seed);
        return new PlayerProgression(new ExperienceService(Rules, new FormulaEvaluator(random), random), Rules);
    }

    private static PlayerState Warrior(int experience) => new(1, "Tester", 3001, level: 1, "warrior", "human", sex: 1)
    {
        Experience = experience,
        Constitution = 16,
        MaxHitPoints = 20,
        MaxMana = 100,
        MaxMovement = 100
    };

    [Fact]
    public void Gain_UpdatesExperience()
    {
        var player = Warrior(0);

        var result = Progression().GainExperience(player, 100);

        Assert.False(result.LeveledUp);
        Assert.Equal(100, player.Experience);
        Assert.Equal(1, player.Level);
    }

    [Fact]
    public void LevelUp_RaisesLevelMaximumsAndTitle()
    {
        var player = Warrior(2600);

        var result = Progression().GainExperience(player, 1000);

        Assert.True(result.LeveledUp);
        Assert.Equal(2, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.InRange(player.MaxHitPoints, 20 + 9, 20 + 13); // 1d5 + con/2
        Assert.Equal(100, player.MaxMana); // no mana when advancing from level 1
        Assert.Equal(103, player.MaxMovement);
        Assert.Equal("the #wHuman#N Swordpupil", player.Title);
        Assert.Matches("^You (rise|gain|advance) a level!$", result.Messages[0]);
    }

    [Fact]
    public void UnknownClass_Throws()
    {
        var player = Warrior(0);
        player.CharacterClass = "necromancer";

        var error = Assert.Throws<InvalidOperationException>(() => Progression().GainExperience(player, 10));

        Assert.Contains("unknown class 'necromancer'", error.Message);
    }
}

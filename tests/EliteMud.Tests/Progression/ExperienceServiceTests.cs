using EliteMud.Application.Progression;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Progression;

/// <summary>
/// The legacy limits.c rules: exp_needed, gain_exp, advance_level / improve_abilities and set_title, on the real
/// class and race content. Random outcomes are checked over many seeds.
/// </summary>
public class ExperienceServiceTests
{
    private static readonly RulesContent Rules =
        RulesContentLoader.Load(Path.Combine(RealWorld.RepositoryRoot, "content")).Rules;

    private static readonly ClassDefinition Warrior = Rules.Classes["warrior"];
    private static readonly ClassDefinition MagicUser = Rules.Classes["magic-user"];
    private static readonly RaceDefinition Human = Rules.Races["human"];

    private static ExperienceService Service(int seed = 1)
    {
        var random = new Random(seed);
        return new ExperienceService(Rules, new FormulaEvaluator(random), random);
    }

    [Theory]
    [InlineData(1, 2666)]
    [InlineData(30, 1000000)]
    [InlineData(108, 40000000)]
    public void ExpNeeded_UsesTheClassCurve(int level, int expected)
    {
        Assert.Equal(expected, Service().ExpNeeded(Warrior, level));
    }

    [Theory]
    // needed / (10 + level/15): 2666 / 10 at level 1, 1000000 / 12 at level 30
    [InlineData(1, 266)]
    [InlineData(30, 83333)]
    public void Gain_IsCappedPerGain(int level, int cap)
    {
        var gain = Service().GainExperience(Warrior, level, 0, 5_000_000);

        Assert.Equal(cap, gain.Change);
        Assert.Equal(cap, gain.Experience);
        Assert.Equal($"You receive {cap} experience points.", Assert.Single(gain.Messages));
    }

    [Fact]
    public void GainOfOne_IsLousy()
    {
        var gain = Service().GainExperience(Warrior, 1, 0, 1);

        Assert.Equal("You receive 1 lousy experience point.", Assert.Single(gain.Messages));
    }

    [Fact]
    public void GainOverTheThreshold_LevelsUpAndResetsExperience()
    {
        var gain = Service().GainExperience(Warrior, 1, 2600, 266);

        Assert.True(gain.LeveledUp);
        Assert.Equal(0, gain.Experience);
        Assert.Matches("^You (rise|gain|advance) a level!$", Assert.Single(gain.Messages));
    }

    [Fact]
    public void GainReachingTheThresholdExactly_DoesNotLevelUp()
    {
        var gain = Service().GainExperience(Warrior, 1, 2400, 266);

        Assert.False(gain.LeveledUp);
        Assert.Equal(2666, gain.Experience);
    }

    [Theory]
    [InlineData(109)] // the mortal cap
    [InlineData(110)] // immortal
    public void NoGainAtTheTop(int level)
    {
        var gain = Service().GainExperience(Warrior, level, 500, 1000);

        Assert.Equal(500, gain.Experience);
        Assert.Empty(gain.Messages);
    }

    [Theory]
    [InlineData(100, -30, 70, "You lose 30 experience points.")]
    [InlineData(100, -1, 99, "You lose 1 experience point.")]
    [InlineData(10, -50, 0, "You lose 10 experience points.")]
    [InlineData(0, -5, 0, "You have no experience to lose.")]
    [InlineData(100, int.MinValue, 0, "You lose 100 experience points.")]
    public void Loss_NeverGoesBelowZero(int experience, int amount, int expected, string message)
    {
        var gain = Service().GainExperience(Warrior, 109, experience, amount); // losses apply at 109 too

        Assert.Equal(expected, gain.Experience);
        Assert.Equal(message, Assert.Single(gain.Messages));
    }

    [Fact]
    public void AdvanceLevel_UsesTheClassProfile()
    {
        var abilities = new AbilityScores(16, 0, 12, 12, 12, 16, 12);
        for (var seed = 0; seed < 200; seed++)
        {
            // fighter profile: hp 1d5 + con/2, mana 5 except at level 1, move 3
            var atOne = Service(seed).AdvanceLevel(Warrior, Human, 1, abilities);
            Assert.InRange(atOne.HitPointGain, 9, 13);
            Assert.Equal(0, atOne.ManaGain);
            Assert.Equal(3, atOne.MovementGain);

            Assert.Equal(5, Service(seed).AdvanceLevel(Warrior, Human, 2, abilities).ManaGain);
        }
    }

    [Fact]
    public void AbilityRolls_RaiseEachStatByAtMostOneUpToTheRaceMaximum()
    {
        var start = new AbilityScores(17, 0, 17, 17, 17, 17, 17);
        var improved = new HashSet<string>();
        for (var seed = 0; seed < 500; seed++)
        {
            var result = Service(seed).AdvanceLevel(Warrior, Human, 100, start).Abilities;
            foreach (var (stat, before, after) in Pairs(start, result))
            {
                Assert.InRange(after, before, Math.Min(before + 1, 18)); // human maximum is 18
                if (after > before)
                {
                    improved.Add(stat);
                }
            }
        }

        Assert.Equal(6, improved.Count);
    }

    [Fact]
    public void AbilityRolls_StopAtTheRaceMaximum()
    {
        var atMax = new AbilityScores(18, 100, 18, 18, 18, 18, 18);
        for (var seed = 0; seed < 200; seed++)
        {
            var result = Service(seed).AdvanceLevel(Warrior, Human, 100, atMax);

            Assert.Equal(atMax, result.Abilities);
            Assert.Empty(result.Messages);
        }
    }

    [Fact]
    public void AtLevelOne_OnlyPrimeStatsCanImprove()
    {
        // number(25, 100) < 1 never holds; number(0, 100) < 1 only on 0
        var start = new AbilityScores(10, 0, 10, 10, 10, 10, 10);
        for (var seed = 0; seed < 500; seed++)
        {
            var result = Service(seed).AdvanceLevel(Warrior, Human, 1, start).Abilities;

            Assert.Equal(start with { Strength = result.Strength, Constitution = result.Constitution }, result);
        }
    }

    [Fact]
    public void StrengthBonus_OnlyForItsClassesAndNeverAbove100()
    {
        var strong = new AbilityScores(18, 98, 10, 10, 10, 10, 10);
        var warriorGrew = false;
        for (var seed = 0; seed < 500; seed++)
        {
            var warrior = Service(seed).AdvanceLevel(Warrior, Human, 100, strong).Abilities;
            Assert.InRange(warrior.StrengthAdd, 98, 100);
            warriorGrew |= warrior.StrengthAdd > 98;

            Assert.Equal(98, Service(seed).AdvanceLevel(MagicUser, Human, 100, strong).Abilities.StrengthAdd);
        }

        Assert.True(warriorGrew);
    }

    [Theory]
    [InlineData(1, Sex.Male, "the #wHuman#N Swordpupil")]
    [InlineData(3, Sex.Female, "the #wHuman#N Recruit")] // row 3/3 + 1 = 2
    public void Title_UsesRaceTitleNameAndTheRowForTheLevel(int level, Sex sex, string expected)
    {
        Assert.Equal(expected, ExperienceService.Title(Warrior, Human, sex, level));
    }

    [Fact]
    public void Title_UsesTheFemaleTitleForFemales()
    {
        // magic-user row 4 (levels 9-11): Spell-delver / Spell-delvress
        Assert.EndsWith("Spell-delver", ExperienceService.Title(MagicUser, Human, Sex.Male, 9));
        Assert.EndsWith("Spell-delvress", ExperienceService.Title(MagicUser, Human, Sex.Female, 9));
    }

    private static IEnumerable<(string Stat, int Before, int After)> Pairs(AbilityScores before, AbilityScores after)
    {
        yield return ("str", before.Strength, after.Strength);
        yield return ("int", before.Intelligence, after.Intelligence);
        yield return ("wis", before.Wisdom, after.Wisdom);
        yield return ("dex", before.Dexterity, after.Dexterity);
        yield return ("con", before.Constitution, after.Constitution);
        yield return ("cha", before.Charisma, after.Charisma);
    }
}

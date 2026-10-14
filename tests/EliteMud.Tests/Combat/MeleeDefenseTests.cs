using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Combat;

/// <summary>
/// The victim's side of a melee hit (legacy hit(), fight.c:1482–1489 and 1523–1551), for a mob as for a player
/// (#42 decision 11).
/// </summary>
public class MeleeDefenseTests
{
    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly CombatCalculator _calculator;
    private readonly MobInstance _mob;

    public MeleeDefenseTests()
    {
        var formulas = new FormulaEvaluator(new Random(1));
        var metadata = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        _calculator = new CombatCalculator(new DodgeSkill(metadata, formulas), new ParrySkill(metadata, formulas));
        _mob = new MobInstance(1, new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null));
    }

    /// <summary>
    /// Legacy dam *= (3 + POS_FIGHTING - pos) / 3 in integer math, as the C code computes it (its comments say
    /// x1.33 and x1.66 for sitting and resting).
    /// </summary>
    [Theory]
    [InlineData(Position.Standing, 9)]
    [InlineData(Position.Sitting, 9)] // 4 / 3 = 1
    [InlineData(Position.Resting, 9)] // 5 / 3 = 1
    [InlineData(Position.Sleeping, 18)] // 6 / 3 = 2
    [InlineData(Position.Stunned, 18)] // 7 / 3 = 2
    public void AMobNotOnItsFeet_TakesMoreDamage(Position position, int damage)
    {
        _mob.Position = position;

        var result = _calculator.DefendAgainstMelee(_mob, 9);

        Assert.Equal(damage, result.Damage);
        Assert.False(result.Dodged); // mobs have no skills yet (#72)
    }

    /// <summary>A melee hit that lands does at least 1 (legacy MAX(1, dam)), e.g. a weak attacker's bare hands.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AHitThatLands_DoesAtLeastOne(int damage)
    {
        Assert.Equal(1, _calculator.DefendAgainstMelee(_mob, damage).Damage);
    }
}

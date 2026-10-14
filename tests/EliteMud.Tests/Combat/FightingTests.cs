using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Combat;

/// <summary>
/// Who fights whom is a reference to the opponent (legacy ch->specials.fighting), for players and mobs alike (#62).
/// </summary>
public class FightingTests
{
    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly CombatCalculator _calculator;
    private readonly PlayerState _player = new(1, "Tester", 3001, level: 10, "warrior", "human", sex: 1);
    private readonly MobInstance _mob;

    public FightingTests()
    {
        var formulas = new FormulaEvaluator();
        var metadata = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        _calculator = new CombatCalculator(new DodgeSkill(metadata, formulas), new ParrySkill(metadata, formulas));
        _mob = new MobInstance(1, new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null));
    }

    [Fact]
    public void APlayer_StartsFighting_InTheFightingPosition()
    {
        _player.Position = Position.Resting;

        _calculator.SetFighting(_player, _mob);

        Assert.Same(_mob, _player.Fighting);
        Assert.Equal(Position.Fighting, _player.Position);
    }

    [Fact]
    public void AMob_StartsFighting_InItsOwnPosition()
    {
        _calculator.SetFighting(_mob, _player);

        Assert.Same(_player, _mob.Fighting);
        Assert.Equal(Position.Standing, _mob.Position); // legacy set_fighting only moves players
    }

    [Fact]
    public void StartingASecondFight_IsAMistake()
    {
        _calculator.SetFighting(_player, _mob);

        Assert.Throws<InvalidOperationException>(() => _calculator.SetFighting(_player, _mob));
    }

    [Fact]
    public void StoppingAFight_StandsUp()
    {
        _calculator.SetFighting(_player, _mob);

        _calculator.StopFighting(_player);

        Assert.Null(_player.Fighting);
        Assert.Equal(Position.Standing, _player.Position);
    }

    /// <summary>Legacy stop_fighting stands everyone up: someone bashed down gets up when the fight ends.</summary>
    [Fact]
    public void StoppingAFight_StandsUpAMobThatWasKnockedDown()
    {
        _calculator.SetFighting(_mob, _player);
        _mob.Position = Position.Sitting;

        _calculator.StopFighting(_mob);

        Assert.Equal(Position.Standing, _mob.Position);
    }

    [Theory]
    [InlineData(Position.Stunned)]
    [InlineData(Position.Incapacitated)]
    [InlineData(Position.MortallyWounded)]
    public void StoppingAFight_LeavesTheDyingDown(Position position)
    {
        _calculator.SetFighting(_player, _mob);
        _player.Position = position;

        _calculator.StopFighting(_player);

        Assert.Equal(position, _player.Position);
    }

    /// <summary>
    /// When someone leaves (flees, quits), everyone fighting them stops, players and mobs alike; other fights go on
    /// (legacy extract_char).
    /// </summary>
    [Fact]
    public void WhenSomeoneLeaves_EveryoneFightingThemStops_AndNoOtherFights()
    {
        var other = new PlayerState(2, "Other", 3001, level: 10, "warrior", "human", sex: 1);
        var bystander = new PlayerState(3, "Bystander", 3001, level: 10, "warrior", "human", sex: 1);
        var otherMob = new MobInstance(2, _mob.Definition);
        _calculator.SetFighting(other, _player);
        _calculator.SetFighting(_mob, _player);
        _calculator.SetFighting(bystander, otherMob);

        _calculator.StopEveryoneFighting(_player, new ICombatant[] { other, bystander, _mob, otherMob });

        Assert.Null(other.Fighting);
        Assert.Equal(Position.Standing, other.Position);
        Assert.Null(_mob.Fighting);
        Assert.Same(otherMob, bystander.Fighting);
    }
}

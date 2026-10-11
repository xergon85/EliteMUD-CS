using EliteMud.Application.Progression;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Progression;

/// <summary>
/// Experience from fighting (legacy fight.c damage() and die()), for players and mobs.
/// </summary>
public class CombatExperienceTests
{
    private static readonly RulesContent Rules =
        RulesContentLoader.Load(Path.Combine(RealWorld.RepositoryRoot, "content")).Rules;

    private readonly ExperienceService _experience;
    private readonly CombatExperience _combat;

    public CombatExperienceTests()
    {
        var random = new Random(1);
        _experience = new ExperienceService(Rules, new FormulaEvaluator(random), random);
        _combat = new CombatExperience(new PlayerProgression(_experience, Rules), _experience);
    }

    [Fact]
    public void DamageToAMob_AddsVictimLevelTimesDamageOverTwo()
    {
        var player = Player(level: 5);
        var mob = Mob(level: 10, experience: 0);

        _combat.DamageDealt(player, mob, 7);

        Assert.Equal(35, player.Experience); // 10 * 7 / 2, added directly with no cap or message
    }

    [Fact]
    public void DamageBetweenPlayers_GivesNothing()
    {
        var attacker = Player(level: 5);

        _combat.DamageDealt(attacker, Player(level: 20), 50);

        Assert.Equal(0, attacker.Experience);
    }

    [Fact]
    public void ImmortalAttacker_GainsNothingFromDamage()
    {
        var immortal = Player(level: 110);

        _combat.DamageDealt(immortal, Mob(level: 10, experience: 0), 50);

        Assert.Equal(0, immortal.Experience);
    }

    [Fact]
    public void MobDamagingAPlayer_GainsExperience()
    {
        var mob = Mob(level: 10, experience: 100);

        _combat.DamageDealt(mob, Player(level: 8), 20);

        Assert.Equal(180, mob.Experience); // 100 + 8 * 20 / 2
    }

    [Fact]
    public void KillingAMob_GivesAQuarterOfItsExperience()
    {
        var player = Player(level: 20);
        var mob = Mob(level: 10, experience: 4000);

        var result = _combat.Killed(player, mob);

        Assert.Equal(1000, player.Experience);
        Assert.Equal("You receive 1000 experience points.", Assert.Single(result.Messages));
    }

    [Fact]
    public void KillingAMobWithNoExperience_GivesOne()
    {
        var player = Player(level: 20);

        var result = _combat.Killed(player, Mob(level: 1, experience: 0));

        Assert.Equal(1, player.Experience);
        Assert.Equal("You receive 1 lousy experience point.", Assert.Single(result.Messages));
    }

    [Fact]
    public void AMobThatHurtPlayers_IsWorthMore()
    {
        var mob = Mob(level: 10, experience: 4000);
        _combat.DamageDealt(mob, Player(level: 20), 40); // + 400

        var player = Player(level: 20);
        _combat.Killed(player, mob);

        Assert.Equal(1100, player.Experience); // 4400 / 4
    }

    [Fact]
    public void MobKillingAPlayer_GainsACappedShare()
    {
        var mob = Mob(level: 10, experience: 0);
        var victim = Player(level: 50);
        victim.Experience = 4_000_000;

        var result = _combat.Killed(mob, victim);

        // Capped like a player's gain on the warrior curve for the mob's level; no messages for mobs
        var cap = _experience.ExpNeeded(Rules.Classes["warrior"], 10) / 10;
        Assert.Equal(cap, mob.Experience);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void ImmortalMob_GainsNothingFromKills()
    {
        var mob = Mob(level: 115, experience: 0);

        _combat.Killed(mob, Player(level: 50));

        Assert.Equal(0, mob.Experience);
    }

    [Fact]
    public void Death_CostsHalfTheExperienceForTheNextLevel()
    {
        var victim = Player(level: 1);
        victim.Experience = 2000;

        var result = _combat.Died(victim);

        Assert.Equal(667, victim.Experience); // warrior level 1 needs 2666; loses 1333
        Assert.Equal("You lose 1333 experience points.", Assert.Single(result.Messages));
    }

    [Fact]
    public void Death_NeverTakesExperienceBelowZero()
    {
        var victim = Player(level: 30);
        victim.Experience = 100;

        _combat.Died(victim);

        Assert.Equal(0, victim.Experience);
    }

    [Fact]
    public void KillThatLevelsUp_RequestsASave()
    {
        var player = Player(level: 1);
        player.Experience = 2600;

        var result = _combat.Killed(player, Mob(level: 5, experience: 4000));

        Assert.True(result.LeveledUp);
        Assert.Equal(2, player.Level);
        Assert.True(player.SaveRequested);
    }

    private static PlayerState Player(int level) => new(1, "Tester", 3001, (byte)level, "warrior", "human", sex: 1)
    {
        Constitution = 16
    };

    private static MobInstance Mob(int level, int experience) => new(1, new MobDefinition(
        100, "rat", "a rat", "A rat is here.", "Desc", level, "Animal", "None",
        new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
        new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null,
        Experience: experience));
}

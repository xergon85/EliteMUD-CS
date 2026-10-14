using EliteMud.Application.Combat;
using EliteMud.Application.Combat.DamageSteps;
using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Combat;

/// <summary>
/// Each step of <see cref="CombatDamage"/> on its own (#42). <see cref="DamagePathTests"/> covers them together, as
/// each damage source sees them.
/// </summary>
public class DamageStepTests
{
    private const int Room = 3002;
    private const int Temple = CharacterStart.StartRoom;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly CombatCalculator _calculator;
    private readonly CombatExperience _experience;
    private readonly WorldState _world;
    private readonly MobInstance _mob;
    private readonly PlayerState _player;
    private readonly PlayerState _victim;

    public DamageStepTests()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator(new Random(1));
        var skills = new SkillMetadataRegistry(ContentLoader.LoadSkills(Content));
        _calculator = new CombatCalculator(new DodgeSkill(skills, formulas), new ParrySkill(skills, formulas));
        var experienceService = new ExperienceService(rules, formulas, new Random(1));
        _experience = new CombatExperience(new PlayerProgression(experienceService, rules), experienceService);

        // Level 5, alignment -1000, 400 XP: a neutral killer moves 62 towards good and takes 100
        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 100, -1000, new List<MobAttack>(), null,
            Experience: 400);
        _mob = new MobInstance(1, mobDefinition);

        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                [Room] = new(Room, "Arena", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = mobDefinition },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Temple] = new(), [Room] = new() { _mob } },
            new Dictionary<int, List<ObjectInstance>> { [Temple] = new(), [Room] = new() },
            new List<ZoneDefinition>());

        _player = new PlayerState(1, "Tester", Room, level: 10, "warrior", "human", sex: 1)
        {
            Position = Position.Standing
        };
        _victim = new PlayerState(2, "Victim", Room, level: 5, "warrior", "human", sex: 1)
        {
            HitPoints = 100,
            MaxHitPoints = 100,
            Experience = 5000,
            Position = Position.Standing
        };
    }

    [Fact]
    public void TheStepsRunInLegacyOrder()
    {
        var damage = new CombatDamage(_calculator, _experience, _world, Array.Empty<ICombatListener>());

        Assert.Equal(
            new[]
            {
                typeof(IgnoreCorpse),
                typeof(StartFighting),
                typeof(NotifyHurt),
                typeof(CapDamage),
                typeof(LoseHitPoints),
                typeof(UpdatePosition),
                typeof(PositionMessages),
                typeof(StopFightingBelowStunned),
                typeof(Kill),
                typeof(PlayerDeath),
                typeof(MobDeath)
            },
            damage.Steps.Select(step => step.GetType()));
    }

    [Fact]
    public void IgnoreCorpse_StopsAHitOnSomeoneAlreadyDead()
    {
        _victim.Position = Position.Dead;
        var context = Hit(_player, _victim, 50);

        new IgnoreCorpse().Apply(context);

        Assert.True(context.Stopped);
        Assert.Equal(0, context.Damage);
    }

    [Fact]
    public void IgnoreCorpse_LetsAHitOnTheLivingThrough()
    {
        _victim.Position = Position.MortallyWounded;
        var context = Hit(_player, _victim, 50);

        new IgnoreCorpse().Apply(context);

        Assert.False(context.Stopped);
        Assert.Equal(50, context.Damage);
    }

    [Fact]
    public void StartFighting_StartsTheFightBothWays_EvenOnAMiss()
    {
        new StartFighting(_calculator).Apply(Hit(_player, _mob, 0));

        Assert.Same(_mob, _player.Fighting);
        Assert.Same(_player, _mob.Fighting);
    }

    [Fact]
    public void StartFighting_LeavesSomeoneAlreadyFightingOnTheirOpponent()
    {
        _calculator.SetFighting(_mob, _victim);

        new StartFighting(_calculator).Apply(Hit(_player, _mob, 10));

        Assert.Same(_mob, _player.Fighting);
        Assert.Same(_victim, _mob.Fighting);
    }

    [Fact]
    public void StartFighting_NoFightWithYourself()
    {
        new StartFighting(_calculator).Apply(Hit(_player, _player, 10));

        Assert.Null(_player.Fighting);
    }

    [Fact]
    public void NotifyHurt_TellsEveryListener_ButNotAboutHurtingYourself()
    {
        var listener = new RecordingListener();
        var step = new NotifyHurt(new[] { listener });

        step.Apply(Hit(_player, _mob, 0));
        step.Apply(Hit(_player, _player, 10));

        Assert.Equal(new[] { (_player as ICombatant, _mob as ICombatant) }, listener.Hurts);
    }

    [Theory]
    [InlineData(600, 500)]
    [InlineData(-5, 0)]
    [InlineData(50, 50)]
    public void CapDamage_KeepsDamageBetween0And500(int damage, int capped)
    {
        var context = Hit(_player, _mob, damage);

        new CapDamage().Apply(context);

        Assert.Equal(capped, context.Damage);
    }

    [Fact]
    public void LoseHitPoints_TakesTheHitPoints_AndGivesDamageExperience()
    {
        _player.Experience = 0;

        new LoseHitPoints(_experience).Apply(Hit(_player, _mob, 30));

        Assert.Equal(70, _mob.HitPoints);
        Assert.Equal(5 * 30 / 2, _player.Experience);
    }

    [Theory]
    [InlineData(0, Position.Stunned, false)]
    [InlineData(-4, Position.Incapacitated, false)]
    [InlineData(-8, Position.MortallyWounded, false)]
    [InlineData(-11, Position.Dead, true)]
    public void UpdatePosition_APlayersPositionFollowsTheirHitPoints(int hitPoints, Position position, bool died)
    {
        _victim.HitPoints = (short)hitPoints;
        var context = Hit(_player, _victim, 10);

        new UpdatePosition(_calculator).Apply(context);

        Assert.Equal(position, _victim.Position);
        Assert.Equal(died, context.VictimDied);
    }

    [Fact]
    public void UpdatePosition_AMobDiesAtZeroHitPoints()
    {
        _mob.HitPoints = 0;
        var context = Hit(_player, _mob, 10);

        new UpdatePosition(_calculator).Apply(context);

        Assert.Equal(Position.Dead, _mob.Position);
        Assert.True(context.VictimDied);
    }

    [Fact]
    public void UpdatePosition_AMissNeverKills()
    {
        _mob.HitPoints = 0;
        var context = Hit(_player, _mob, 0);

        new UpdatePosition(_calculator).Apply(context);

        Assert.Equal(Position.Standing, _mob.Position);
        Assert.False(context.VictimDied);
    }

    [Fact]
    public void PositionMessages_TellTheVictimAndTheRoom()
    {
        _victim.Position = Position.Incapacitated;
        var context = Hit(_player, _victim, 10);

        new PositionMessages(_world).Apply(context);

        Assert.Equal(
            new[]
            {
                "A:$N is incapacitated and will slowly die, if not aided.",
                "O:$N is incapacitated and will slowly die, if not aided.",
                "V:You are incapacitated and will slowly die, if not aided."
            },
            Lines(context));
    }

    [Fact]
    public void PositionMessages_HurtingYourself_TellsTheRoomButNotYouTwice()
    {
        _victim.Position = Position.Stunned;
        var context = Hit(_victim, _victim, 10);

        new PositionMessages(_world).Apply(context);

        Assert.Equal(
            new[]
            {
                "O:$N is stunned, but will probably regain consciousness again.",
                "V:You're stunned, but will probably regain consciousness again."
            },
            Lines(context));
    }

    [Fact]
    public void PositionMessages_ABleedingPlayerIsToldEvenOnAMiss()
    {
        _victim.HitPoints = 10;
        var context = Hit(_player, _victim, 0);

        new PositionMessages(_world).Apply(context);

        Assert.Equal(new[] { "V:You wish that your wounds would stop BLEEDING so much!" }, Lines(context));
    }

    [Fact]
    public void PositionMessages_NoneForAMob()
    {
        _mob.HitPoints = 1;
        var context = Hit(_player, _mob, 99);

        new PositionMessages(_world).Apply(context);

        Assert.Empty(context.Messages);
    }

    [Fact]
    public void StopFightingBelowStunned_TheIncapacitatedStop_TheirAttackerFightsOn()
    {
        _calculator.SetFighting(_player, _victim);
        _calculator.SetFighting(_victim, _player);
        _victim.Position = Position.Incapacitated;

        new StopFightingBelowStunned(_calculator).Apply(Hit(_player, _victim, 10));

        Assert.Null(_victim.Fighting);
        Assert.Same(_victim, _player.Fighting);
    }

    [Fact]
    public void StopFightingBelowStunned_TheStunnedFightOn()
    {
        _calculator.SetFighting(_victim, _player);
        _victim.Position = Position.Stunned;

        new StopFightingBelowStunned(_calculator).Apply(Hit(_player, _victim, 10));

        Assert.Same(_player, _victim.Fighting);
    }

    [Fact]
    public void Kill_DoesNothingWhileTheVictimLives()
    {
        _calculator.SetFighting(_player, _mob);
        var context = Hit(_player, _mob, 10);

        Kill(new RecordingListener()).Apply(context);

        Assert.Same(_mob, _player.Fighting);
        Assert.Empty(context.Messages);
    }

    [Fact]
    public void Kill_EndsTheFight_MovesAlignment_GivesKillExperience_ThenTheDeathLines()
    {
        _calculator.SetFighting(_player, _mob);
        _calculator.SetFighting(_mob, _player);
        _mob.Position = Position.Dead;
        var listener = new RecordingListener();
        var context = Died(Hit(_player, _mob, 10));

        Kill(listener).Apply(context);

        Assert.Null(_player.Fighting);
        Assert.Null(_mob.Fighting);
        Assert.Equal(62, _player.Alignment);
        Assert.True(_player.SaveRequested);
        Assert.Equal(new[] { (_player as ICombatant, _mob as ICombatant) }, listener.Kills);
        Assert.Equal(
            new[] { "A:$N is DEAD!!", "O:$N is dead!  R.I.P.", "A:You receive 100 experience points." },
            Lines(context));
    }

    [Fact]
    public void Kill_YourselfEarnsNothing()
    {
        _victim.Position = Position.Dead;
        var context = Died(Hit(_victim, _victim, 10));

        Kill(new RecordingListener()).Apply(context);

        Assert.Equal(5000, _victim.Experience);
        Assert.Equal(0, _victim.Alignment);
        Assert.Equal(new[] { "O:$N is dead!  R.I.P.", "V:You are dead!  Sorry..." }, Lines(context));
    }

    [Fact]
    public void PlayerDeath_LeavesACorpse_TakesThePenalty_AndRespawnsAtTheTemple()
    {
        _victim.HitPoints = -20;
        _victim.Position = Position.Dead;
        var context = Died(Hit(_player, _victim, 10));

        new PlayerDeath(_experience, _world).Apply(context);

        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Equal(Temple, _victim.RoomId);
        Assert.Equal(Position.Standing, _victim.Position);
        Assert.Equal(100, _victim.HitPoints);
        Assert.Equal(5000 - 3333, _victim.Experience);
        Assert.True(_victim.SaveRequested);
        Assert.Equal(new[] { "V:You lose 3333 experience points.", "V:You have been resurrected..." }, Lines(context));
    }

    [Fact]
    public void PlayerDeath_IsOnlyForPlayers()
    {
        new PlayerDeath(_experience, _world).Apply(Died(Hit(_player, _mob, 10)));

        Assert.Empty(_world.GetObjectsInRoom(Room));
    }

    [Fact]
    public void MobDeath_LeavesACorpse_AndRemovesTheMob()
    {
        new MobDeath(_world).Apply(Died(Hit(_player, _mob, 10)));

        Assert.DoesNotContain(_mob, _world.GetMobsInRoom(Room));
        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
    }

    [Fact]
    public void MobDeath_LeavesTheCorpseWhereTheMobWas_WhoeverKilledIt()
    {
        var killer = new MobInstance(2, _mob.Definition); // in no room at all

        new MobDeath(_world).Apply(Died(Hit(killer, _mob, 10)));

        Assert.DoesNotContain(_mob, _world.GetMobsInRoom(Room));
        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
    }

    private static DamageContext Hit(ICombatant attacker, ICombatant victim, int damage) =>
        new(attacker, victim, damage);

    private static DamageContext Died(DamageContext context)
    {
        context.VictimDied = true;
        return context;
    }

    private Kill Kill(ICombatListener listener) => new(_calculator, _experience, new[] { listener });

    private static IEnumerable<string> Lines(DamageContext context) =>
        context.Messages.Select(message => $"{message.Target.ToString()[0]}:{message.Template}");

    private sealed class RecordingListener : ICombatListener
    {
        public List<(ICombatant, ICombatant)> Hurts { get; } = new();
        public List<(ICombatant, ICombatant)> Kills { get; } = new();

        public void Hurt(ICombatant attacker, ICombatant victim) => Hurts.Add((attacker, victim));

        public void Killed(ICombatant killer, ICombatant victim) => Kills.Add((killer, victim));
    }
}

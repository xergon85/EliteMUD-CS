using EliteMud.Application.Ai;
using EliteMud.Application.Combat;
using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Ai;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Combat;

/// <summary>
/// What each damage source does when it hits and kills (#42): messages and their order, experience, alignment,
/// corpse. Every source should end the same way, as legacy damage() does for all of them.
/// </summary>
public class DamagePathTests
{
    private const int Room = 3002; // not the temple, so a respawn shows
    private const int Temple = CharacterStart.StartRoom;
    private const string Improved = "just improved";

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    [Fact]
    public void KickKillingAMob_EndsTheFight_MovesAlignment_LeavesACorpse_GivesKillExperience()
    {
        var fixture = new Fixture();

        var result = fixture.Kick().Execute(fixture.AgainstMob());

        fixture.AssertMobKilled();
        Assert.Equal(
            new[]
            {
                "A:your kick hits $N [50]",
                "V:$n kicks you! [50]",
                "O:$n kicks $N!",
                "A:$N is DEAD!!",
                "O:$N is dead!  R.I.P.",
                "A:You receive 100 experience points."
            },
            Lines(result));
    }

    [Fact]
    public void BashKillingAMob_EndsTheFight_MovesAlignment_LeavesACorpse_GivesKillExperience()
    {
        var fixture = new Fixture();

        var result = fixture.Bash().Execute(fixture.AgainstMob());

        fixture.AssertMobKilled();
        Assert.Equal(
            new[]
            {
                "A:You easily bash $N. [50]",
                "V:$n easily bashes you! [50]",
                "O:$n easily bashes $N!",
                "A:$N is DEAD!!",
                "O:$N is dead!  R.I.P.",
                "A:You receive 100 experience points."
            },
            Lines(result));
    }

    [Fact]
    public void BackstabKillingASleepingMob_EndsTheFight_MovesAlignment_LeavesACorpse_GivesKillExperience()
    {
        var fixture = new Fixture();
        fixture.Mob.Position = Position.Sleeping; // a sleeping victim is always hit

        var result = fixture.Backstab().Execute(fixture.AgainstMob());

        fixture.AssertMobKilled();
        var lines = Lines(result);
        Assert.StartsWith("A:you backstab $N! [", lines[0]);
        Assert.Equal(new[] { "A:$N is DEAD!!", "O:$N is dead!  R.I.P.", "A:You receive 100 experience points." },
            lines[^3..]);
    }

    /// <summary>
    /// A player killed by a skill (#40): kill experience for the killer, then legacy die(): a corpse where they
    /// fell, the death penalty, and back to the temple on full hit points.
    /// </summary>
    [Fact]
    public void KickKillingAPlayer_LeavesACorpse_AndRespawnsThemAtTheTemple()
    {
        var fixture = new Fixture();
        var victim = fixture.VictimPlayer;
        var killerExperience = fixture.Attacker.Experience;

        var result = fixture.Kick().Execute(fixture.AgainstPlayer());

        Assert.Contains(fixture.World.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Equal(Temple, victim.RoomId);
        Assert.True(victim.SaveRequested);
        Assert.Equal(Position.Standing, victim.Position);
        Assert.Equal(fixture.World.GetTotalEffectiveMaxHitPoints(victim), victim.HitPoints);
        Assert.Equal(5000 - 3333, victim.Experience);
        Assert.True(fixture.Attacker.Experience >= killerExperience + 1250);
        Assert.Null(fixture.Attacker.FightingConnectionId);
        Assert.Null(victim.FightingConnectionId);
        Assert.Equal(
            new[]
            {
                "A:your kick hits $N [50]",
                "V:$n kicks you! [50]",
                "O:$n kicks $N!",
                "A:$N is DEAD!!",
                "O:$N is dead!  R.I.P.",
                "V:You are dead!  Sorry...",
                "A:You receive 1250 experience points.",
                "V:You lose 3333 experience points.",
                "V:You have been resurrected..."
            },
            Lines(result));
    }

    /// <summary>
    /// Legacy damage() gives kill experience and moves alignment only when ch != victim (fight.c:1034). A player
    /// who kills themselves still dies (corpse, penalty, respawn), with no reward and no killer's line.
    /// </summary>
    [Fact]
    public void KillingYourself_DiesWithoutAKillReward()
    {
        var fixture = new Fixture();
        var player = fixture.Attacker;
        player.HitPoints = 5;
        player.Experience = 100_000;

        var outcome = fixture.Damage().Apply(player, 1, player, 1, 50);

        Assert.True(outcome.VictimDied);
        Assert.Equal(Temple, player.RoomId);
        Assert.Contains(fixture.World.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.True(player.Experience < 100_000); // the penalty, and no kill share
        Assert.Equal(0, player.Alignment);
        var lines = outcome.Messages.Select(message => $"{message.Target.ToString()[0]}:{message.Template}").ToList();
        Assert.Equal("O:$N is dead!  R.I.P.", lines[0]);
        Assert.Equal("V:You are dead!  Sorry...", lines[1]);
        Assert.StartsWith("V:You lose ", lines[2]);
        Assert.Equal("V:You have been resurrected...", lines[^1]);
    }

    [Fact]
    public void KillFirstHitKillingAMob_EndsTheFight_MovesAlignment_LeavesACorpse_GivesKillExperience()
    {
        var fixture = new Fixture();

        var result = fixture.Kill().Execute(fixture.AgainstMob());

        fixture.AssertMobKilled();
        Assert.Equal(
            new[] { "A:$N is DEAD!!", "O:$N is dead!  R.I.P.", "A:You receive 100 experience points." },
            Lines(result)[^3..]);
    }

    /// <summary>A MOB_MEMORY mob remembers whoever hurts it, whatever the source (legacy damage()).</summary>
    [Fact]
    public void AMemoryMob_RemembersWhoKicksIt()
    {
        var fixture = new Fixture();
        var definition = fixture.Mob.Definition with { ParsedFlags = MobFlags.Memory };
        var mob = new MobInstance(2, definition) { HitPoints = 1000 };

        fixture.Kick().Execute(new SkillContext(fixture.Attacker, 1, mob, null, null));

        Assert.Contains(fixture.Attacker.Id, mob.Memory);
    }

    [Fact]
    public void AMiss_DoesNotKillAMobAlreadyAtZeroHitPoints()
    {
        var fixture = new Fixture();
        fixture.Mob.HitPoints = 0; // e.g. left behind by the first hit of kill
        var experience = fixture.Attacker.Experience;

        var outcome = fixture.Damage().Apply(fixture.Attacker, 1, fixture.Mob, null, 0);

        Assert.False(outcome.VictimDied);
        Assert.Empty(outcome.Messages);
        Assert.Contains(fixture.Mob, fixture.World.GetMobsInRoom(Room));
        Assert.Equal(experience, fixture.Attacker.Experience);
        Assert.Equal(0, fixture.Attacker.Alignment);
    }

    /// <summary>Messages as "A:"/"V:"/"O:"/"R:" + template, without skill-improvement lines (random).</summary>
    private static List<string> Lines(SkillResult result) =>
        result.Messages
            .Where(message => !message.Template.Contains(Improved))
            .Select(message => $"{message.Target.ToString()[0]}:{message.Template}")
            .ToList();

    private sealed class Fixture
    {
        private readonly SkillRegistry _skills;
        private readonly CombatCalculator _calculator;
        private readonly CombatExperience _experience;

        public Fixture()
        {
            var rules = RulesContentLoader.Load(Content).Rules;
            var formulas = new FormulaEvaluator(new Random(1));

            // Every skill hits for 50; these tests are about what happens after the hit, not the rolls
            var skills = ContentLoader.LoadSkills(Content).ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var type in new[] { SkillType.Kick, SkillType.Bash, SkillType.Backstab })
            {
                var skill = skills[(int)type];
                skills[(int)type] = skill with
                {
                    Mechanics = new SkillMechanics
                    {
                        HitFormula = "return true",
                        DamageFormula = "return 50",
                        DamageMultiplierFormula = skill.Mechanics?.DamageMultiplierFormula,
                        Requirements = skill.Mechanics?.Requirements
                    }
                };
            }

            var metadata = new SkillMetadataRegistry(skills);
            _skills = new SkillRegistry(metadata, formulas);
            _calculator = new CombatCalculator(new DodgeSkill(metadata, formulas), new ParrySkill(metadata, formulas));
            var experienceService = new ExperienceService(rules, formulas, new Random(1));
            _experience = new CombatExperience(new PlayerProgression(experienceService, rules), experienceService);

            // Alignment -1000 moves a neutral killer 62 towards good; 400 XP gives a 100 kill share
            var mobDefinition = new MobDefinition(
                100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
                new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
                new List<string>(), new List<string>(), 10, 10, -1000, new List<MobAttack>(), null,
                Experience: 400);
            Mob = new MobInstance(1, mobDefinition);

            World = new WorldState(
                new WorldDefinition(new Dictionary<int, RoomDefinition>
                {
                    [Temple] = new(Temple, "Temple", "Desc", new List<ExitDefinition>()),
                    [Room] = new(Room, "Arena", "Desc", new List<ExitDefinition>())
                }),
                new Dictionary<int, MobDefinition> { [100] = mobDefinition },
                new Dictionary<int, ObjectDefinition>(),
                new Dictionary<int, List<MobInstance>> { [Temple] = new(), [Room] = new() { Mob } },
                new Dictionary<int, List<ObjectInstance>> { [Temple] = new(), [Room] = new() },
                new List<ZoneDefinition>());

            Attacker = new PlayerState(1, "Tester", Room, level: 10, "thief", "human", sex: 1)
            {
                Damroll = 50, // the first hit of kill does at least 50 too
                Position = Position.Standing
            };
            foreach (var type in new[] { SkillType.Kick, SkillType.Bash, SkillType.Backstab })
            {
                Attacker.SetSkill(type, 100);
            }

            VictimPlayer = new PlayerState(2, "Victim", Room, level: 5, "warrior", "human", sex: 1)
            {
                HitPoints = 5,
                Experience = 5000,
                Position = Position.Standing
            };
        }

        public WorldState World { get; }
        public MobInstance Mob { get; }
        public PlayerState Attacker { get; }
        public PlayerState VictimPlayer { get; }

        public SkillContext AgainstMob() => new(Attacker, 1, Mob, null, null);
        public SkillContext AgainstPlayer() => new(Attacker, 1, VictimPlayer, 2, null);

        public CombatDamage Damage() => new(_calculator, _experience, World);

        public KickSkillExecutor Kick() => new(_skills, new CombatDamage(_calculator, _experience, World));
        public BashSkillExecutor Bash() => new(_skills, new CombatDamage(_calculator, _experience, World));
        public BackstabSkillExecutor Backstab() =>
            new(_skills, _calculator, World, new CombatDamage(_calculator, _experience, World));

        public MeleeAttackExecutor Kill() =>
            new(_calculator, World, new MobAiService(new MockScriptEngine(), new PathfindingService()), Damage());

        public void AssertMobKilled()
        {
            Assert.Equal(Position.Dead, Mob.Position);
            Assert.DoesNotContain(Mob, World.GetMobsInRoom(Room));
            Assert.Contains(World.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
            Assert.Null(Attacker.FightingConnectionId);
            Assert.Null(Mob.FightingConnectionId);
            Assert.Equal(62, Attacker.Alignment);
            Assert.True(Attacker.Experience > 100); // damage XP + the 100 kill share
        }
    }
}

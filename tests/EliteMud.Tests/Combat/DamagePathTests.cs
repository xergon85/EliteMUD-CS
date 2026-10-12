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
    private const int Room = 3001;
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
    /// Today's bug (#40): a player killed by a skill is left dead where they fell, with no corpse, no respawn,
    /// no penalty and no kill experience. Changes when player death joins the shared path.
    /// </summary>
    [Fact]
    public void KickKillingAPlayer_LeavesThemDeadInTheRoom()
    {
        var fixture = new Fixture();
        var victim = fixture.VictimPlayer;
        var victimExperience = victim.Experience;

        var result = fixture.Kick().Execute(fixture.AgainstPlayer());

        Assert.Equal(Position.Dead, victim.Position);
        Assert.Equal(Room, victim.RoomId);
        Assert.Equal(victimExperience, victim.Experience);
        Assert.DoesNotContain(fixture.World.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Null(fixture.Attacker.FightingConnectionId);
        Assert.Null(victim.FightingConnectionId);
        Assert.Equal(new[] { "A:$N is DEAD!!", "O:$N is dead!  R.I.P." }, Lines(result)[^2..]);
    }

    /// <summary>
    /// Today's bug (#42): the first hit of kill has no death handling, so a mob it kills stays in the room at
    /// ≤0 hp. Changes when kill joins the shared path.
    /// </summary>
    [Fact]
    public void KillFirstHitKillingAMob_LeavesItInTheRoom()
    {
        var fixture = new Fixture();

        var result = fixture.Kill().Execute(fixture.AgainstMob());

        Assert.True(result.Success);
        Assert.True(fixture.Mob.HitPoints <= 0);
        Assert.Contains(fixture.Mob, fixture.World.GetMobsInRoom(Room));
        Assert.DoesNotContain(fixture.World.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.DoesNotContain(Lines(result), line => line.Contains("DEAD"));
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
                    [Room] = new(Room, "Temple", "Desc", new List<ExitDefinition>())
                }),
                new Dictionary<int, MobDefinition> { [100] = mobDefinition },
                new Dictionary<int, ObjectDefinition>(),
                new Dictionary<int, List<MobInstance>> { [Room] = new() { Mob } },
                new Dictionary<int, List<ObjectInstance>> { [Room] = new() },
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

        public KickSkillExecutor Kick() => new(_skills, new CombatDamage(_calculator, _experience, World));
        public BashSkillExecutor Bash() => new(_skills, new CombatDamage(_calculator, _experience, World));
        public BackstabSkillExecutor Backstab() =>
            new(_skills, _calculator, World, new CombatDamage(_calculator, _experience, World));

        public MeleeAttackExecutor Kill() =>
            new(_calculator, World, new MobAiService(new MockScriptEngine(), new PathfindingService()), _experience);

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

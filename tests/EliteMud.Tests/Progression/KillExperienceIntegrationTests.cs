using EliteMud.Application.Combat;
using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Progression;

/// <summary>
/// A kick that kills a mob goes through the shared kill-reward path: kill XP from the mob's experience, the
/// legacy messages after the death messages, and a level-up with a save request.
/// </summary>
public class KillExperienceIntegrationTests
{
    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    [Fact]
    public void KickingAMobToDeath_GivesKillExperienceAndLevelsUp()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator(new Random(1));

        // Kick always hits here; this tests the reward, not the roll (random(1,101) can miss even at 100%)
        var skills = ContentLoader.LoadSkills(Content).ToDictionary(pair => pair.Key, pair => pair.Value);
        var kickId = (int)SkillType.Kick;
        var kick = skills[kickId];
        skills[kickId] = kick with
        {
            Mechanics = new SkillMechanics { DamageFormula = kick.Mechanics!.DamageFormula, HitFormula = "return true" }
        };
        var metadata = new SkillMetadataRegistry(skills);
        var calculator = new CombatCalculator(new DodgeSkill(metadata, formulas), new ParrySkill(metadata, formulas));

        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 1, 0, new List<MobAttack>(), null,
            Experience: 4000);
        var mob = new MobInstance(1, mobDefinition);
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition> { [3001] = new(3001, "Temple", "Desc", new List<ExitDefinition>()) }),
            new Dictionary<int, MobDefinition> { [100] = mobDefinition },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [3001] = new() { mob } },
            new Dictionary<int, List<ObjectInstance>> { [3001] = new() },
            new List<ZoneDefinition>());

        var experience = new ExperienceService(rules, formulas, new Random(1));
        var combatExperience = new CombatExperience(new PlayerProgression(experience, rules), experience);
        var executor = new KickSkillExecutor(
            new SkillRegistry(metadata, formulas),
            new CombatDamage(calculator, combatExperience, world, Array.Empty<ICombatListener>()));

        var player = new PlayerState(1, "Tester", 3001, level: 1, "warrior", "human", sex: 1)
        {
            Experience = 2600, // level 1 needs 2666; the capped kill share (266) crosses it
            Constitution = 16
        };
        player.SetSkill(SkillType.Kick, 100);

        var result = executor.Execute(new SkillContext(player, 1, mob, null, null));

        Assert.True(result.Success);
        Assert.Equal(Position.Dead, mob.Position);
        Assert.Equal(2, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.True(player.SaveRequested);
        Assert.Equal("the #wHuman#N Swordpupil", player.Title);

        var actorLines = result.Messages.Where(m => m.Target == SkillMessageTarget.Actor).Select(m => m.Template).ToList();
        var dead = actorLines.IndexOf("$N is DEAD!!");
        Assert.True(dead >= 0);
        Assert.Matches("^You (rise|gain|advance) a level!$", actorLines[dead + 1]);
    }
}

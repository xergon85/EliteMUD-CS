using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Progression;
using EliteMud.Application.Skills;
using EliteMud.Application.Spells;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Cast;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Tests.Ai;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Combat;

/// <summary>
/// What a damage spell does when it hits and kills (#42), as each player in the room sees it. Magic missile
/// always hits, for 1 + level/2 (6 at level 10).
/// </summary>
public sealed class CastDamageTests : IDisposable
{
    private const int Room = 3002; // not the temple, so a respawn shows
    private const int Temple = CharacterStart.StartRoom;
    private const string Improved = "has improved";

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");

    private readonly MobInstance _mob;
    private readonly WorldState _world;
    private readonly TestConnection _caster;
    private readonly TestConnection _victim;
    private readonly TestConnection _observer;
    private readonly CastCommandHandler _cast;
    private readonly CombatCalculator _calculator;

    public CastDamageTests()
    {
        var rules = RulesContentLoader.Load(Content).Rules;
        var formulas = new FormulaEvaluator(new Random(1));
        var spellMetadata = new SpellMetadataRegistry(ContentLoader.LoadSpells(Content));
        // Dodge and parry always activate here, so a test can tell whether a hit goes through them
        var skills = ContentLoader.LoadSkills(Content).ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var type in new[] { SkillType.Dodge, SkillType.Parry })
        {
            var skill = skills[(int)type];
            skills[(int)type] = skill with
            {
                Mechanics = new SkillMechanics
                {
                    ActivationFormula = "return true",
                    EffectFormula = skill.Mechanics?.EffectFormula,
                    Requirements = skill.Mechanics?.Requirements
                }
            };
        }

        var skillMetadata = new SkillMetadataRegistry(skills);
        _calculator = new CombatCalculator(new DodgeSkill(skillMetadata, formulas), new ParrySkill(skillMetadata, formulas));
        var experience = new ExperienceService(rules, formulas, new Random(1));

        // Alignment -1000 moves a neutral killer 62 towards good; 400 XP gives a 100 kill share
        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 1, -1000, new List<MobAttack>(), null,
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

        var caster = new PlayerState(1, "Caster", Room, level: 10, "magic-user", "human", sex: 1)
        {
            Mana = 100,
            Position = Position.Standing
        };
        caster.SetSpell(SpellType.MagicMissile, 90);

        _caster = new TestConnection(1, caster);
        _victim = new TestConnection(2, new PlayerState(2, "Victim", Room, level: 5, "warrior", "human", sex: 1)
        {
            HitPoints = 100,
            Experience = 5000,
            Position = Position.Standing
        });
        _observer = new TestConnection(3, new PlayerState(3, "Observer", Room, level: 5, "warrior", "human", sex: 1));

        var connections = new ConnectionRegistry();
        connections.SetProvider(() => new[] { _caster.Context, _victim.Context, _observer.Context });

        _cast = new CastCommandHandler(
            new SpellRegistry(spellMetadata, formulas),
            spellMetadata,
            _world,
            connections,
            new ActMessageService(),
            new CombatDamage(
                _calculator,
                new CombatExperience(new PlayerProgression(experience, rules), experience),
                _world,
                Array.Empty<ICombatListener>()),
            new SkillMessageSender(
                new ActMessageService(),
                connections,
                new LookCommandHandler(_world, new MockScriptEngine(), connections, RulesContent.Empty)));
    }

    public void Dispose()
    {
        _caster.Dispose();
        _victim.Dispose();
        _observer.Dispose();
    }

    private PlayerState Caster => _caster.Context.Player;
    private PlayerState Victim => _victim.Context.Player;

    [Fact]
    public async Task KillingAMob_EndsTheFight_MovesAlignment_LeavesACorpse_GivesKillExperience()
    {
        await Cast("'magic missile' rat");

        Assert.Equal(Position.Dead, _mob.Position);
        Assert.DoesNotContain(_mob, _world.GetMobsInRoom(Room));
        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Null(Caster.Fighting);
        Assert.Equal(62, Caster.Alignment);
        Assert.Equal(
            new[] { "Your magic missile hits a rat! [6]", "A rat is DEAD!!", "You receive 100 experience points." },
            CasterLines());
        Assert.Equal(
            new[] { "Caster casts magic missile at a rat! [6]", "A rat is dead!  R.I.P." },
            _observer.Lines());
    }

    [Fact]
    public async Task HittingAPlayer_StartsTheFight()
    {
        await Cast("'magic missile' victim");

        Assert.Equal(94, Victim.HitPoints);
        Assert.Same(Victim, Caster.Fighting);
        Assert.Same(Caster, Victim.Fighting);
        Assert.Equal(new[] { "Your magic missile hits Victim! [6]" }, CasterLines());
        Assert.Equal(new[] { "Caster casts magic missile at you! [6]" }, _victim.Lines());
        Assert.Equal(new[] { "Caster casts magic missile at Victim! [6]" }, _observer.Lines());
    }

    /// <summary>
    /// Legacy has dodge and parry in the melee hit only (fight.c hit()), not in damage(): spells can't be avoided.
    /// </summary>
    [Fact]
    public async Task HittingAPlayer_CannotBeDodgedOrParried()
    {
        Victim.Level = 30;
        Victim.SetSkill(SkillType.Dodge, 100);
        Victim.SetSkill(SkillType.Parry, 100);

        // The same victim dodges melee damage: the test setup really activates dodge
        var meleeVictim = new PlayerState(9, "Melee", Room, level: 30, "warrior", "human", sex: 1)
        {
            HitPoints = 100,
            Position = Position.Standing
        };
        meleeVictim.SetSkill(SkillType.Dodge, 100);
        Assert.True(_calculator.ApplyDamage(meleeVictim, 6).Dodged);

        await Cast("'magic missile' victim");

        Assert.Equal(94, Victim.HitPoints);
        Assert.Equal(new[] { "Caster casts magic missile at you! [6]" }, _victim.Lines());
    }

    /// <summary>
    /// The extra damage against a victim who isn't on their feet is part of the melee hit (fight.c hit()), not
    /// damage(). Only a victim already fighting can be hit sitting: one who isn't is set fighting (and on their
    /// feet) before the damage, in legacy set_fighting too.
    /// </summary>
    [Fact]
    public async Task HittingASittingPlayerInAFight_DoesNoExtraDamage()
    {
        Victim.Fighting = _observer.Context.Player; // e.g. bashed while fighting the observer
        Victim.Position = Position.Sitting;
        Assert.Equal(8, _calculator.CalculateDamageWithPositionMultiplier(6, Position.Sitting)); // melee: 4/3

        await Cast("'magic missile' victim");

        Assert.Equal(94, Victim.HitPoints);
        Assert.Equal(new[] { "Caster casts magic missile at you! [6]" }, _victim.Lines());
    }

    /// <summary>
    /// Legacy update_pos (fight.c): below 0 hp a player is stunned, incapacitated or mortally wounded; dead only
    /// below -10. The old cast path made every player at 0 hp or below dead.
    /// </summary>
    [Theory]
    [InlineData(6, 0, Position.Stunned)]
    [InlineData(3, -3, Position.Incapacitated)]
    [InlineData(0, -6, Position.MortallyWounded)]
    public async Task HittingAPlayerToZeroOrBelow_FollowsLegacyPositions(int hitPoints, int left, Position position)
    {
        Victim.HitPoints = (short)hitPoints;

        await Cast("'magic missile' victim");

        Assert.Equal(left, Victim.HitPoints);
        Assert.Equal(position, Victim.Position);
        Assert.Equal(new[] { "Your magic missile hits Victim! [6]" }, CasterLines());
        Assert.Equal(new[] { "Caster casts magic missile at you! [6]" }, _victim.Lines());
    }

    /// <summary>
    /// A player killed by a spell (#40): kill experience for the caster, then legacy die(): a corpse where they
    /// fell, the death penalty, and back to the temple on full hit points.
    /// </summary>
    [Fact]
    public async Task KillingAPlayer_LeavesACorpse_AndRespawnsThemAtTheTemple()
    {
        Caster.Level = 30; // 16 damage: 3 hp - 16 is below -10, dead rather than incapacitated
        Victim.HitPoints = 3;

        await Cast("'magic missile' victim");

        Assert.Contains(_world.GetObjectsInRoom(Room), obj => obj.Definition.Name.Contains("corpse"));
        Assert.Equal(Temple, Victim.RoomId);
        Assert.True(Victim.SaveRequested);
        Assert.Equal(Position.Standing, Victim.Position);
        Assert.Equal(5000 - 3333, Victim.Experience);
        Assert.Equal(
            new[] { "Your magic missile hits Victim! [16]", "Victim is DEAD!!", "You receive 1250 experience points." },
            CasterLines());
        Assert.Equal(
            new[]
            {
                "Caster casts magic missile at you! [16]",
                "You are dead!  Sorry...",
                "You lose 3333 experience points.",
                "You have been resurrected..."
            },
            _victim.Lines());
        Assert.Equal(
            new[] { "Caster casts magic missile at Victim! [16]", "Victim is dead!  R.I.P." },
            _observer.Lines());
    }

    private async Task Cast(string argument) =>
        await _cast.HandleAsync(new CommandRequest("cast", argument, null), _caster.Context, CancellationToken.None);

    /// <summary>The caster's lines, without proficiency improvement (random).</summary>
    private List<string> CasterLines() => _caster.Lines().Where(line => !line.Contains(Improved)).ToList();
}

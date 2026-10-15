using EliteMud.Application.Commands.Stat;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Commands;

/// <summary>The stat command shows the same effective stats and THAC0 that combat uses (#80, #88).</summary>
public class StatHandlerTests
{
    private const int Room = 3002;

    private static readonly string Content = Path.Combine(RealWorld.RepositoryRoot, "content");
    private static readonly RulesContent Rules = RulesContentLoader.Load(Content).Rules;

    private readonly StatHandler _stat;

    public StatHandlerTests()
    {
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Room] = new(Room, "Arena", "Desc", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Room] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Room] = new() },
            new List<ZoneDefinition>());
        _stat = new StatHandler(Rules, new EffectiveStats(Rules));
    }

    /// <summary>Legacy do_stat shows get_thaco(): a level 30 warrior's 20 − 30 / 3, with no hitroll taken off.</summary>
    [Fact]
    public void Stat_ShowsTheClassThac0()
    {
        var player = new PlayerState(1, "Tester", Room, level: 30, "warrior", "human", sex: 1) { Hitroll = 5 };

        Assert.Contains("THAC0[#R10#N]", _stat.Handle(player).Message);
    }

    /// <summary>A stat shows its effective value, kept at the race's maximum: a human with 17 and +3 shows 18.</summary>
    [Fact]
    public void Stat_ShowsAnAbilityWithinTheRacesMaximum()
    {
        var player = new PlayerState(1, "Tester", Room, level: 30, "warrior", "human", sex: 1) { Strength = 17 };
        player.AddAffect(new Affect
        {
            Type = AffectType.Bless,
            Location = AffectLocation.Strength,
            Modifier = 3,
            DurationHours = 10
        });

        Assert.Contains("Str: [#C18#N (#C17#N+3)]", _stat.Handle(player).Message);
    }
}

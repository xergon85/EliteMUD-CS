using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Tests.Ai;

namespace EliteMud.Tests.Server;

/// <summary>
/// The act messages of a combat result reach the right players, whether the actor is a player or a mob (the
/// combat tick), and the victim can be shown the room they fled to.
/// </summary>
public sealed class SkillMessageSenderTests : IDisposable
{
    private const int Arena = 1;
    private const int Alley = 2;

    private readonly MobInstance _mob;
    private readonly TestConnection _victim;
    private readonly TestConnection _observer;
    private readonly TestConnection _elsewhere;
    private readonly SkillMessageSender _sender;

    public SkillMessageSenderTests()
    {
        var mobDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);
        _mob = new MobInstance(1, mobDefinition);
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Arena] = new(Arena, "Arena", "Sand.", new List<ExitDefinition>()),
                [Alley] = new(Alley, "Alley", "A narrow alley.", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = mobDefinition },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Arena] = new() { _mob }, [Alley] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Arena] = new(), [Alley] = new() },
            new List<ZoneDefinition>());

        _victim = new TestConnection(1, new PlayerState(1, "Victim", Arena, level: 5, "warrior", "human", sex: 1));
        _observer = new TestConnection(2, new PlayerState(2, "Observer", Arena, level: 5, "warrior", "human", sex: 1));
        _elsewhere = new TestConnection(3, new PlayerState(3, "Elsewhere", Alley, level: 5, "warrior", "human", sex: 1));

        var connections = new ConnectionRegistry();
        connections.SetProvider(() => new[] { _victim.Context, _observer.Context, _elsewhere.Context });
        _sender = new SkillMessageSender(
            new ActMessageService(),
            connections,
            new LookCommandHandler(world, new MockScriptEngine(), connections, RulesContent.Empty));
    }

    public void Dispose()
    {
        _victim.Dispose();
        _observer.Dispose();
        _elsewhere.Dispose();
    }

    private PlayerState Victim => _victim.Context.Player;

    [Fact]
    public async Task AMobsMessages_ReachTheVictim_AndTheOthersInItsRoom()
    {
        await _sender.SendAsync(
            new[]
            {
                new SkillMessage(SkillMessageTarget.Actor, "You bite $N."),
                new SkillMessage(SkillMessageTarget.Victim, "$n bites you.", Victim),
                new SkillMessage(SkillMessageTarget.Others, "$n bites $N.", Victim)
            },
            _mob,
            CancellationToken.None);

        Assert.Equal(new[] { "A rat bites you." }, _victim.Lines());
        Assert.Equal(new[] { "A rat bites Victim." }, _observer.Lines());
        Assert.Empty(_elsewhere.Lines());
    }

    [Fact]
    public async Task VictimLooks_ShowsTheVictimTheRoomTheyAreInNow()
    {
        Victim.RoomId = Alley; // fled

        await _sender.SendAsync(
            new[] { new SkillMessage(SkillMessageTarget.VictimLooks, string.Empty, Victim) },
            _mob,
            CancellationToken.None);

        var lines = _victim.Lines();
        Assert.Equal("Alley", lines[0]);
        Assert.Contains("A narrow alley.", lines);
        Assert.Empty(_observer.Lines());
    }
}

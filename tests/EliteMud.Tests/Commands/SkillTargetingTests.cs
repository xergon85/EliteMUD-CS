using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Skills;
using EliteMud.Tests.Ai;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Commands;

/// <summary>
/// A skill finds a player to use it on among those in the game and in the room (#79).
/// </summary>
public sealed class SkillTargetingTests : IDisposable
{
    private const int Arena = 1;
    private const int Alley = 2;

    private readonly WorldState _world;
    private readonly ConnectionRegistry _connections = new();
    private readonly RecordingExecutor _kick = new(TargetingMode.CurrentFightTarget);
    private readonly RecordingExecutor _backstab = new(TargetingMode.RequiredInRoom);
    private readonly TestConnection _actor;
    private readonly TestConnection _rival;
    private readonly TestConnection _elsewhere;

    public SkillTargetingTests()
    {
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Arena] = new(Arena, "Arena", "Sand.", new List<ExitDefinition>()),
                [Alley] = new(Alley, "Alley", "A narrow alley.", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Arena] = new(), [Alley] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Arena] = new(), [Alley] = new() },
            new List<ZoneDefinition>());

        _actor = Connect(1, "Actor", Arena);
        _rival = Connect(2, "Rival", Arena);
        _elsewhere = Connect(3, "Elsewhere", Alley);
    }

    public void Dispose()
    {
        _actor.Dispose();
        _rival.Dispose();
        _elsewhere.Dispose();
    }

    private PlayerState Actor => _actor.Context.Player;

    [Theory]
    [InlineData(TargetingMode.CurrentFightTarget)]
    [InlineData(TargetingMode.RequiredInRoom)]
    public async Task ANamedPlayerInTheRoom_IsTheVictim(TargetingMode mode)
    {
        var executor = mode == TargetingMode.CurrentFightTarget ? _kick : _backstab;

        await Use(executor, "riv");

        Assert.Same(_rival.Context.Player, executor.Victim);
    }

    [Theory]
    [InlineData(TargetingMode.CurrentFightTarget, "Actor")]
    [InlineData(TargetingMode.CurrentFightTarget, "Elsewhere")]
    [InlineData(TargetingMode.RequiredInRoom, "Actor")]
    [InlineData(TargetingMode.RequiredInRoom, "Elsewhere")]
    public async Task YourselfOrAPlayerInAnotherRoom_IsNotFound(TargetingMode mode, string name)
    {
        var executor = mode == TargetingMode.CurrentFightTarget ? _kick : _backstab;

        await Use(executor, name);

        Assert.False(executor.Ran);
        Assert.Equal(new[] { $"You don't see '{name}' here." }, _actor.Lines());
    }

    [Fact]
    public async Task WithoutAName_TheOpponentInTheRoomIsTheVictim()
    {
        Actor.Fighting = _rival.Context.Player;

        await Use(_kick, null);

        Assert.Same(_rival.Context.Player, _kick.Victim);
    }

    [Fact]
    public async Task AnOpponentWhoLeftTheGame_IsNotHere()
    {
        _world.Population.Leave(_rival.Context.Player);
        Actor.Fighting = _rival.Context.Player; // a stale opponent: leaving would have stopped the fight

        await Use(_kick, null);

        Assert.False(_kick.Ran);
        Assert.Equal(new[] { "They aren't here." }, _actor.Lines());
    }

    private Task Use(RecordingExecutor executor, string? argument)
    {
        var sender = new SkillMessageSender(
            TestActMessenger.For(_world, _connections),
            _connections,
            new LookCommandHandler(_world, new MockScriptEngine(), RulesContent.Empty));
        var handler = new SkillCommandHandler(executor, _world, sender);
        return handler.HandleAsync(new CommandRequest("skill", argument, null), _actor.Context, CancellationToken.None)
            .AsTask();
    }

    private TestConnection Connect(int id, string name, int room)
    {
        var connection = new TestConnection(id, new PlayerState(id, name, room, level: 5, "warrior", "human", sex: 1));
        _connections.Add(connection.Context);
        _world.Population.Enter(connection.Context.Player, room);
        return connection;
    }

    private sealed class RecordingExecutor : ISkillExecutor
    {
        public RecordingExecutor(TargetingMode targeting) => Targeting = targeting;

        public SkillType SkillType => SkillType.Kick;

        public TargetingMode Targeting { get; }

        public bool Ran { get; private set; }

        public ICombatant? Victim { get; private set; }

        public SkillResult Execute(SkillContext context)
        {
            Ran = true;
            Victim = context.Victim;
            return SkillResult.Succeeded();
        }
    }
}

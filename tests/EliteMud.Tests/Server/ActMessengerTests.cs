using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Tests.Server;

/// <summary>
/// Act messages reach the players in the actor's room that each target names, worded for each of them, whether
/// the actor and victim are players or mobs (#79).
/// </summary>
public sealed class ActMessengerTests : IDisposable
{
    private const int Arena = 1;
    private const int Alley = 2;

    private readonly MobInstance _rat;
    private readonly TestConnection _actor;
    private readonly TestConnection _victim;
    private readonly TestConnection _observer;
    private readonly TestConnection _elsewhere;
    private readonly ActMessenger _act;

    public ActMessengerTests()
    {
        var ratDefinition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);
        _rat = new MobInstance(1, ratDefinition);
        var world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Arena] = new(Arena, "Arena", "Sand.", new List<ExitDefinition>()),
                [Alley] = new(Alley, "Alley", "A narrow alley.", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition> { [100] = ratDefinition },
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Arena] = new() { _rat }, [Alley] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Arena] = new(), [Alley] = new() },
            new List<ZoneDefinition>());

        _actor = new TestConnection(1, new PlayerState(1, "Actor", Arena, level: 5, "warrior", "human", sex: 1));
        _victim = new TestConnection(2, new PlayerState(2, "Victim", Arena, level: 5, "warrior", "human", sex: 2));
        _observer = new TestConnection(3, new PlayerState(3, "Observer", Arena, level: 5, "warrior", "human", sex: 1));
        _elsewhere = new TestConnection(4, new PlayerState(4, "Elsewhere", Alley, level: 5, "warrior", "human", sex: 1));

        var connections = new ConnectionRegistry();
        foreach (var connection in new[] { _actor, _victim, _observer, _elsewhere })
        {
            connections.Add(connection.Context);
            world.Population.Enter(connection.Context.Player, connection.Context.Player.RoomId);
        }

        _act = TestActMessenger.For(world, connections);
    }

    public void Dispose()
    {
        _actor.Dispose();
        _victim.Dispose();
        _observer.Dispose();
        _elsewhere.Dispose();
    }

    private PlayerState Actor => _actor.Context.Player;
    private PlayerState Victim => _victim.Context.Player;

    [Fact]
    public void ToChar_ReachesOnlyTheActor()
    {
        _act.ToChar(Actor, "$n poke $N.", Victim);

        Assert.Equal(new[] { "You poke Victim." }, _actor.Lines());
        AssertNothingFor(_victim, _observer, _elsewhere);
    }

    [Fact]
    public void ToVict_ReachesOnlyTheVictim()
    {
        _act.ToVict(Actor, "$n pokes $N.", Victim);

        Assert.Equal(new[] { "Actor pokes you." }, _victim.Lines());
        AssertNothingFor(_actor, _observer, _elsewhere);
    }

    [Fact]
    public void ToRoom_ReachesEveryoneInTheActorsRoom_TheActorIncluded()
    {
        _act.ToRoom(Actor, "$n shouts.");

        Assert.Equal(new[] { "You shouts." }, _actor.Lines());
        Assert.Equal(new[] { "Actor shouts." }, _victim.Lines());
        Assert.Equal(new[] { "Actor shouts." }, _observer.Lines());
        Assert.Empty(_elsewhere.Lines());
    }

    [Fact]
    public void ToNotChar_ReachesTheRoomButNotTheActor()
    {
        _act.ToNotChar(Actor, "$n pokes $N.", Victim);

        Assert.Equal(new[] { "Actor pokes you." }, _victim.Lines());
        Assert.Equal(new[] { "Actor pokes Victim." }, _observer.Lines());
        AssertNothingFor(_actor, _elsewhere);
    }

    [Fact]
    public void ToNotVict_ReachesTheRoomButNotTheActorOrVictim()
    {
        _act.ToNotVict(Actor, "$n pokes $N.", Victim);

        Assert.Equal(new[] { "Actor pokes Victim." }, _observer.Lines());
        AssertNothingFor(_actor, _victim, _elsewhere);
    }

    [Fact]
    public void BroadcastToRoom_SendsTheLineAsItIs_ToTheRoomButNotTheActor()
    {
        _act.BroadcastToRoom(Actor, "Actor sits down, $n.");

        Assert.Equal(new[] { "Actor sits down, $n." }, _victim.Lines());
        Assert.Equal(new[] { "Actor sits down, $n." }, _observer.Lines());
        AssertNothingFor(_actor, _elsewhere);
    }

    [Fact]
    public void AMobActor_IsSeenByThePlayersInItsRoom_AndToCharGoesNowhere()
    {
        _act.ToChar(_rat, "You bite $N.", Victim);
        _act.ToVict(_rat, "$n bites you.", Victim);
        _act.ToNotVict(_rat, "$n bites $N.", Victim);

        Assert.Equal(new[] { "A rat bites you." }, _victim.Lines());
        Assert.Equal(new[] { "A rat bites Victim." }, _actor.Lines());
        Assert.Equal(new[] { "A rat bites Victim." }, _observer.Lines());
        Assert.Empty(_elsewhere.Lines());
    }

    [Fact]
    public void AMobVictim_IsNamedToTheRoom_AndToVictGoesNowhere()
    {
        _act.ToVict(Actor, "$n kicks you.", _rat);
        _act.ToNotVict(Actor, "$n kicks $N.", _rat);

        Assert.Equal(new[] { "Actor kicks a rat." }, _victim.Lines());
        Assert.Equal(new[] { "Actor kicks a rat." }, _observer.Lines());
        AssertNothingFor(_actor, _elsewhere);
    }

    private static void AssertNothingFor(params TestConnection[] connections)
    {
        foreach (var connection in connections)
        {
            Assert.Empty(connection.Lines());
        }
    }
}

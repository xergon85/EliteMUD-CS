using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Reply;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Tell;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Commands;

/// <summary>
/// A tell reaches the player named, and a reply goes back to the player who sent the last tell, as long as that
/// same character is still playing (legacy do_reply, act.comm.c:262) (#79).
/// </summary>
public sealed class TellReplyCommandTests : IDisposable
{
    private const int Room = 1;

    private readonly WorldState _world;
    private readonly ConnectionRegistry _connections = new();
    private readonly TellCommandHandler _tell;
    private readonly ReplyCommandHandler _reply;
    private readonly List<TestConnection> _opened = new();
    private readonly TestConnection _alice;
    private readonly TestConnection _bob;

    public TellReplyCommandTests()
    {
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Room] = new(Room, "Room", "A room.", new List<ExitDefinition>())
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Room] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Room] = new() },
            new List<ZoneDefinition>());
        var output = new SessionCharacterOutput(_connections);
        _tell = new TellCommandHandler(_world, output);
        _reply = new ReplyCommandHandler(_world, output);

        _alice = LogIn(1, "Alice");
        _bob = LogIn(2, "Bob");
    }

    public void Dispose()
    {
        foreach (var connection in _opened)
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task Tell_ReachesThePlayerNamed_AndReplyGoesBack()
    {
        await Run(_tell, _alice, "bob hi there");
        await Run(_reply, _bob, "hello");

        Assert.Equal(new[] { "You tell Bob 'hi there'", "Bob tells you 'hello'" }, _alice.Lines());
        Assert.Equal(new[] { "Alice tells you 'hi there'", "You tell Alice 'hello'" }, _bob.Lines());
    }

    [Fact]
    public async Task Tell_ToSomeoneNotPlaying_Fails()
    {
        await Run(_tell, _alice, "carol hi");

        Assert.Equal(new[] { "No such player around." }, _alice.Lines());
    }

    [Fact]
    public async Task Reply_WithNoTellYet_HasNoOneToReplyTo_EvenWithoutAMessage()
    {
        await Run(_reply, _bob, "");

        Assert.Equal(new[] { "You have no-one to reply to!" }, _bob.Lines());
    }

    [Fact]
    public async Task Reply_WithoutAMessage_AsksForOne()
    {
        await Run(_tell, _alice, "bob hi");
        _bob.Lines();

        await Run(_reply, _bob, "");

        Assert.Equal(new[] { "What is your reply?" }, _bob.Lines());
    }

    [Fact]
    public async Task Reply_ToSomeoneWhoLoggedOutAndBackIn_FindsThemNoLongerPlaying()
    {
        await Run(_tell, _alice, "bob hi");
        _bob.Lines();
        LogOut(_alice);
        var aliceAgain = LogIn(1, "Alice");

        await Run(_reply, _bob, "still there?");

        Assert.Equal(new[] { "They are no longer playing." }, _bob.Lines());
        Assert.Empty(aliceAgain.Lines());
    }

    private static Task Run(ICommandHandler handler, TestConnection sender, string argument) =>
        handler.HandleAsync(new CommandRequest("tell", argument, null), sender.Context, CancellationToken.None)
            .AsTask();

    private TestConnection LogIn(int id, string name)
    {
        var connection = new TestConnection(id, new PlayerState(id, name, Room, level: 5, "warrior", "human", sex: 1));
        _opened.Add(connection);
        _connections.Add(connection.Context);
        _world.Population.Enter(connection.Context.Player, Room);
        return connection;
    }

    private void LogOut(TestConnection connection)
    {
        _world.Population.Leave(connection.Context.Player);
        _connections.Remove(connection.Context);
    }
}

using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Emote;
using EliteMud.Server.Adapters.Commands.Gossip;
using EliteMud.Server.Adapters.Commands.Say;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Shout;
using EliteMud.Tests.Ai;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Commands;

/// <summary>
/// Who hears say, emote, shout and gossip: the room, the zone, or everyone, never the speaker twice (#79).
/// </summary>
public sealed class ChannelCommandTests : IDisposable
{
    private const int Square = 1;
    private const int Market = 2;
    private const int Forest = 3;

    private readonly WorldState _world;
    private readonly ConnectionRegistry _connections = new();
    private readonly TestConnection _speaker;
    private readonly TestConnection _roommate;
    private readonly TestConnection _zonemate;
    private readonly TestConnection _faraway;

    public ChannelCommandTests()
    {
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Square] = new(Square, "Square", "A square.", new List<ExitDefinition>(), ZoneId: 10),
                [Market] = new(Market, "Market", "A market.", new List<ExitDefinition>(), ZoneId: 10),
                [Forest] = new(Forest, "Forest", "A forest.", new List<ExitDefinition>(), ZoneId: 20)
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Square] = new(), [Market] = new(), [Forest] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Square] = new(), [Market] = new(), [Forest] = new() },
            new List<ZoneDefinition>());

        _speaker = Connect(1, "Speaker", Square);
        _roommate = Connect(2, "Roommate", Square);
        _zonemate = Connect(3, "Zonemate", Market);
        _faraway = Connect(4, "Faraway", Forest);
    }

    public void Dispose()
    {
        _speaker.Dispose();
        _roommate.Dispose();
        _zonemate.Dispose();
        _faraway.Dispose();
    }

    [Fact]
    public async Task Say_IsHeardByTheRoom()
    {
        await Run(new SayCommandHandler(new MockScriptEngine(), Act), "say", "hello");

        Assert.Equal(new[] { "You say, 'hello'." }, _speaker.Lines());
        Assert.Equal(new[] { "Speaker says, 'hello'." }, _roommate.Lines());
        AssertNothingFor(_zonemate, _faraway);
    }

    [Fact]
    public async Task Emote_IsSeenByTheRoom()
    {
        await Run(new EmoteCommandHandler(Act), "emote", "waves");

        Assert.Equal(new[] { "Speaker waves " }, _speaker.Lines());
        Assert.Equal(new[] { "Speaker waves " }, _roommate.Lines());
        AssertNothingFor(_zonemate, _faraway);
    }

    [Fact]
    public async Task Shout_IsHeardByTheZone()
    {
        await Run(new ShoutCommandHandler(new SessionCharacterOutput(_connections), _world), "shout", "help");

        Assert.Equal(new[] { "You yell, 'help'" }, _speaker.Lines());
        Assert.Equal(new[] { "Speaker yells, 'help'" }, _roommate.Lines());
        Assert.Equal(new[] { "Speaker yells, 'help'" }, _zonemate.Lines());
        Assert.Empty(_faraway.Lines());
    }

    [Fact]
    public async Task Gossip_IsHeardByEveryone()
    {
        await Run(new GossipCommandHandler(_world, new SessionCharacterOutput(_connections)), "gossip", "hi all");

        Assert.Equal(new[] { "You gossip, 'hi all'" }, _speaker.Lines());
        foreach (var listener in new[] { _roommate, _zonemate, _faraway })
        {
            Assert.Equal(new[] { "Speaker gossips, 'hi all'" }, listener.Lines());
        }
    }

    private ActMessenger Act => TestActMessenger.For(_world, _connections);

    private Task Run(ICommandHandler handler, string verb, string argument) =>
        handler.HandleAsync(new CommandRequest(verb, argument, null), _speaker.Context, CancellationToken.None)
            .AsTask();

    private TestConnection Connect(int id, string name, int room)
    {
        var connection = new TestConnection(id, new PlayerState(id, name, room, level: 5, "warrior", "human", sex: 1));
        _connections.Add(connection.Context);
        _world.Population.Enter(connection.Context.Player, room);
        return connection;
    }

    private static void AssertNothingFor(params TestConnection[] connections)
    {
        foreach (var connection in connections)
        {
            Assert.Empty(connection.Lines());
        }
    }
}

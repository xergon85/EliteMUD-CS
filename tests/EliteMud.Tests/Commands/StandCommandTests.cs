using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Server.Adapters.Commands.Stand;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Commands;

/// <summary>
/// What standing up shows, as legacy do_stand: the room sees "$n clambers to $s feet." with $s filled in.
/// </summary>
public sealed class StandCommandTests : IDisposable
{
    private const int Square = 1;

    private readonly WorldState _world;
    private readonly ConnectionRegistry _connections = new();
    private readonly TestConnection _stander;
    private readonly TestConnection _onlooker;

    public StandCommandTests()
    {
        _world = new WorldState(
            new WorldDefinition(new Dictionary<int, RoomDefinition>
            {
                [Square] = new(Square, "Square", "A square.", new List<ExitDefinition>(), ZoneId: 10)
            }),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition>(),
            new Dictionary<int, List<MobInstance>> { [Square] = new() },
            new Dictionary<int, List<ObjectInstance>> { [Square] = new() },
            new List<ZoneDefinition>());

        _stander = Connect(1, "Stander", sex: 2);
        _onlooker = Connect(2, "Onlooker", sex: 1);
    }

    public void Dispose()
    {
        _stander.Dispose();
        _onlooker.Dispose();
    }

    [Fact]
    public async Task Stand_FromSitting_TheRoomSeesTheirPronoun()
    {
        _stander.Context.Player.Position = Position.Sitting;

        await new StandCommandHandler(TestActMessenger.For(_world, _connections))
            .HandleAsync(new CommandRequest("stand", null, null), _stander.Context, CancellationToken.None);

        Assert.Equal(Position.Standing, _stander.Context.Player.Position);
        Assert.Equal(new[] { "You stand up." }, _stander.Lines());
        Assert.Equal(new[] { "Stander clambers to her feet." }, _onlooker.Lines());
    }

    private TestConnection Connect(int id, string name, byte sex)
    {
        var connection = new TestConnection(id, new PlayerState(id, name, Square, level: 5, "warrior", "human", sex));
        _connections.Add(connection.Context);
        _world.Population.Enter(connection.Context.Player, Square);
        return connection;
    }
}

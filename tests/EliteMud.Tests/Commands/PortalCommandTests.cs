using System.Net;
using System.Net.Sockets;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Portal;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Tests.Commands;

/// <summary>
/// Tests for the enter and activate portal commands.
/// NOTE: Like RecallCommandTests, these use real TCP sockets for TelnetSession.
/// </summary>
public class PortalCommandTests : IDisposable
{
    private const int StartRoom = 12060;
    private const int Destination = 12070;
    private const int TicketId = 12038;

    private readonly List<TcpClient> _clients = new();
    private readonly List<TcpListener> _listeners = new();

    [Fact]
    public async Task Enter_OpenPortal_MovesPlayerToDestination()
    {
        var env = await CreateEnvironment(portalFlags: 0);

        await env.Enter.HandleAsync(new CommandRequest("enter", "entrance", null), env.Context, CancellationToken.None);

        Assert.Equal(Destination, env.Context.Player.RoomId);
    }

    [Fact]
    public async Task Enter_ClosedPortal_PlayerStays()
    {
        var env = await CreateEnvironment(portalFlags: PortalState.ClosedFlag);

        await env.Enter.HandleAsync(new CommandRequest("enter", "entrance", null), env.Context, CancellationToken.None);

        Assert.Equal(StartRoom, env.Context.Player.RoomId);
    }

    [Fact]
    public async Task Enter_BelowMinimumLevel_PlayerStays()
    {
        var env = await CreateEnvironment(portalFlags: 0, minLevel: 50, playerLevel: 10);

        await env.Enter.HandleAsync(new CommandRequest("enter", "entrance", null), env.Context, CancellationToken.None);

        Assert.Equal(StartRoom, env.Context.Player.RoomId);
    }

    [Fact]
    public async Task Activate_WithTicket_OpensPortalAndKeepsTicket()
    {
        var env = await CreateEnvironment(portalFlags: PortalState.ClosedFlag, carryTicket: true);

        await env.Activate.HandleAsync(new CommandRequest("activate", "entrance", null), env.Context, CancellationToken.None);

        Assert.False(env.Portal.IsClosed);
        Assert.Contains(env.WorldState.GetPlayerInventory(env.Context.Player), item => item.Definition.Id == TicketId);
    }

    [Fact]
    public async Task Activate_WithoutTicket_PortalStaysClosed()
    {
        var env = await CreateEnvironment(portalFlags: PortalState.ClosedFlag, carryTicket: false);

        await env.Activate.HandleAsync(new CommandRequest("activate", "entrance", null), env.Context, CancellationToken.None);

        Assert.True(env.Portal.IsClosed);
    }

    private sealed record TestEnvironment(
        EnterCommandHandler Enter,
        ActivateCommandHandler Activate,
        ConnectionContext Context,
        WorldState WorldState,
        PortalState Portal);

    private async Task<TestEnvironment> CreateEnvironment(
        int portalFlags,
        int minLevel = -1,
        byte playerLevel = 20,
        bool carryTicket = false)
    {
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [StartRoom] = new(StartRoom, "Circus Maximus - Entrance", "Desc", new List<ExitDefinition>()),
            [Destination] = new(Destination, "Entrance to Racetrack Seating - South", "Desc", new List<ExitDefinition>())
        };

        var portalDef = new ObjectDefinition(
            12037, "entrance circus", "Circus Maximus entrance", "Long", "Desc", "Portal",
            new List<string>(), new List<string>(), null,
            new List<int> { Destination, portalFlags, TicketId, minLevel, -1, -1 },
            0, 0, new List<ObjectAffect>());
        var ticketDef = new ObjectDefinition(
            TicketId, "ticket circus", "an circus ticket", "Long", "Desc", "Key",
            new List<string> { "Take" }, new List<string>(), null, new List<int>(),
            1, 10, new List<ObjectAffect>());

        var portal = new ObjectInstance(1, portalDef);
        var roomObjects = new List<ObjectInstance> { portal };
        var ticket = new ObjectInstance(2, ticketDef);
        if (carryTicket)
        {
            roomObjects.Add(ticket);
        }

        var worldState = new WorldState(
            new WorldDefinition(rooms),
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition> { [12037] = portalDef, [TicketId] = ticketDef },
            new Dictionary<int, List<MobInstance>>(),
            new Dictionary<int, List<ObjectInstance>> { [StartRoom] = roomObjects },
            new List<ZoneDefinition>());

        var player = new PlayerState(1, "TestPlayer", roomId: StartRoom, level: playerLevel);
        if (carryTicket)
        {
            Assert.True(worldState.TakeObject(player, ticket.InstanceId));
        }

        var (_, stream) = await CreateRealTcpConnection();
        var context = new ConnectionContext(1, new TelnetSession(stream), player, characterId: 1);
        var connectionRegistry = new ConnectionRegistry();
        connectionRegistry.SetProvider(() => new[] { context });

        var lookHandler = new LookCommandHandler(worldState, new MockScriptEngine(), connectionRegistry, RulesContent.Empty);

        return new TestEnvironment(
            new EnterCommandHandler(worldState, lookHandler, connectionRegistry),
            new ActivateCommandHandler(worldState, connectionRegistry),
            context,
            worldState,
            portal.Portal!);
    }

    private async Task<(TcpClient client, NetworkStream stream)> CreateRealTcpConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _listeners.Add(listener);

        var client = new TcpClient();
        _clients.Add(client);
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        _clients.Add(await listener.AcceptTcpClientAsync());

        return (client, client.GetStream());
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        foreach (var listener in _listeners)
        {
            listener.Stop();
        }
    }
}

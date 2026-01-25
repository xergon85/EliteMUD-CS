using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Recall;
using System.Net.Sockets;
using System.Net;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Scripting;

namespace EliteMud.Tests.Commands;

/// <summary>
/// Integration tests for the RecallCommandHandler.
/// Tests the recall command functionality including level restrictions, room restrictions, and combat checks.
/// NOTE: These tests use real TCP sockets to create NetworkStream instances for TelnetSession.
/// </summary>
public class RecallCommandTests : IDisposable
{
    private readonly List<TcpClient> _clients = new();
    private readonly List<TcpListener> _listeners = new();
    
    [Fact]
    public async Task Recall_Level10Character_SuccessfullyTeleportsToTemple()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 10, roomId: 100);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(3001, context.Player.RoomId); // Should be in Temple of Midgaard
    }

    [Fact]
    public async Task Recall_Level11Character_FailsAndStaysInRoom()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 11, roomId: 100);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(100, context.Player.RoomId); // Should stay in original room
    }

    [Fact]
    public async Task Recall_Level50Character_FailsAndStaysInRoom()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 50, roomId: 200);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(200, context.Player.RoomId); // Should stay in original room
    }

    [Fact]
    public async Task Recall_WhileFighting_FailsAndStaysInRoom()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 10, roomId: 100);
        context.Player.FightingConnectionId = 999; // Set as fighting
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(100, context.Player.RoomId); // Should stay in original room
        Assert.Equal(999, context.Player.FightingConnectionId); // Still fighting
    }

    [Fact]
    public async Task Recall_FromGodRoom_FailsAndStaysInRoom()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 10, roomId: 1200, roomFlags: RoomFlags.GodRoom);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(1200, context.Player.RoomId); // Should stay in god room
    }

    [Fact]
    public async Task Recall_FromArena_FailsAndStaysInRoom()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 10, roomId: 1300, roomFlags: RoomFlags.Arena);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(1300, context.Player.RoomId); // Should stay in arena
    }

    [Fact]
    public async Task Recall_Level5Character_SuccessfullyTeleports()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 5, roomId: 500);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(3001, context.Player.RoomId); // Should be in Temple
    }

    [Fact]
    public async Task Recall_Level1Character_SuccessfullyTeleports()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 1, roomId: 1);
        var command = new CommandRequest("recall", null, null);

        // Act
        var result = await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(CommandOutcome.Continue, result);
        Assert.Equal(3001, context.Player.RoomId); // Should be in Temple
    }

    [Fact]
    public async Task Recall_ResetsPositionFromFighting()
    {
        // Arrange
        var (handler, context) = await CreateTestEnvironment(level: 10, roomId: 100);
        context.Player.Position = Position.Fighting;
        context.Player.FightingConnectionId = null; // Not actually fighting (edge case)
        var command = new CommandRequest("recall", null, null);

        // Act
        await handler.HandleAsync(command, context, CancellationToken.None);

        // Assert
        Assert.Equal(Position.Standing, context.Player.Position); // Should reset to standing
        Assert.Equal(3001, context.Player.RoomId);
    }

    private async Task<(RecallCommandHandler handler, ConnectionContext context)> 
        CreateTestEnvironment(byte level, int roomId, RoomFlags roomFlags = RoomFlags.None)
    {
        // Create world with required rooms
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [roomId] = new(roomId, "Test Room", "A test room.", new List<ExitDefinition>(), roomFlags),
            [3001] = new(3001, "Temple of Midgaard", "The temple is peaceful.", new List<ExitDefinition>())
        };
        var worldDef = new WorldDefinition(rooms);

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>>();
        var roomObjs = new Dictionary<int, List<ObjectInstance>>();
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Create player with specified level
        var player = new PlayerState(1, "TestPlayer", roomId: roomId, level: level);
        
        // Create a real TCP connection for NetworkStream
        var (client, stream) = await CreateRealTcpConnection();
        var session = new TelnetSession(stream);
        
        var context = new ConnectionContext(1, session, player, characterId: 1);

        // Create connection registry
        var connectionRegistry = new ConnectionRegistry();
        connectionRegistry.SetProvider(() => new[] { context });

        // Create mock script engine (no-op for tests)
        var scriptEngine = new MockScriptEngine();
        
        // Create look handler (uses same world state)
        var lookHandler = new LookCommandHandler(worldState, scriptEngine, connectionRegistry);

        // Create recall handler
        var handler = new RecallCommandHandler(worldState, lookHandler, connectionRegistry);

        return (handler, context);
    }

    private async Task<(TcpClient client, NetworkStream stream)> CreateRealTcpConnection()
    {
        // Create a listener on a random port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _listeners.Add(listener);
        
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        
        // Connect a client
        var client = new TcpClient();
        _clients.Add(client);
        
        await client.ConnectAsync(IPAddress.Loopback, port);
        
        // Accept the connection on server side
        var serverClient = await listener.AcceptTcpClientAsync();
        _clients.Add(serverClient);
        
        // Return the client's stream
        var stream = client.GetStream();
        
        return (client, stream);
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client?.Dispose();
        }
        foreach (var listener in _listeners)
        {
            listener?.Stop();
        }
    }
}

/// <summary>
/// Mock script engine that does nothing for testing purposes.
/// </summary>
internal class MockScriptEngine : IScriptEngine
{
    public ValueTask ExecuteAsync(ScriptHook hook, ScriptContext context, CancellationToken cancellationToken)
    {
        // No-op for tests
        return ValueTask.CompletedTask;
    }
}

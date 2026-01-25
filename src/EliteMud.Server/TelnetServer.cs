using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.Session.Authentication;
using EliteMud.Application.Session.Login;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server;

internal sealed class TelnetServer
{
    private readonly TcpListener _listener;
    private readonly CommandCatalog _catalog;
    private readonly PromptCatalog _promptCatalog;
    private readonly CommandRouter _commandRouter;
    private readonly ConcurrentDictionary<int, ConnectionContext> _connections = new();
    private int _nextConnectionId;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly AuthenticationHandler _authHandler;
    private readonly IServiceProvider _serviceProvider;
    private readonly IpBanService _ipBanService;
    private readonly IWorldState _worldState;
    private readonly CharacterSaveQueue _saveQueue;

    public TelnetServer(
        IPAddress address,
        int port,
        CommandCatalog catalog,
        PromptCatalog promptCatalog,
        CommandRouter commandRouter,
        ConnectionRegistry connectionRegistry,
        AuthenticationHandler authHandler,
        IServiceProvider serviceProvider,
        IpBanService ipBanService,
        IWorldState worldState,
        CharacterSaveQueue saveQueue)
    {
        _listener = new TcpListener(address, port);
        _catalog = catalog;
        _promptCatalog = promptCatalog;
        _commandRouter = commandRouter;
        _connectionRegistry = connectionRegistry;
        _authHandler = authHandler;
        _serviceProvider = serviceProvider;
        _ipBanService = ipBanService;
        _worldState = worldState;
        _saveQueue = saveQueue;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown initiated
            await ShutdownGracefullyAsync();
        }
        finally
        {
            _listener.Stop();
        }
    }
    
    private async Task ShutdownGracefullyAsync()
    {
        Console.WriteLine("\n=== Server shutdown initiated ===");
        Console.WriteLine($"Saving {_connections.Count} connected players...");
        
        // Notify all connected players
        var disconnectTasks = new List<Task>();
        foreach (var (connectionId, context) in _connections)
        {
            disconnectTasks.Add(NotifyAndDisconnectAsync(connectionId, context));
        }
        
        // Wait for all players to be notified and saved (with timeout)
        var timeoutTask = Task.Delay(5000); // 5 second timeout
        var completedTask = await Task.WhenAny(Task.WhenAll(disconnectTasks), timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            Console.WriteLine("WARNING: Shutdown timeout reached, some players may not have been saved");
        }
        else
        {
            Console.WriteLine($"All {_connections.Count} players saved successfully");
        }
        
        Console.WriteLine("Server shutdown complete");
    }
    
    private async Task NotifyAndDisconnectAsync(int connectionId, ConnectionContext context)
    {
        try
        {
            // Notify player of shutdown
            await context.Session.SendLineAsync("\n\n*** SERVER SHUTTING DOWN ***", CancellationToken.None);
            await context.Session.SendLineAsync("Your character has been saved. Goodbye!", CancellationToken.None);
            
            // Save player state using the save queue
            await _saveQueue.QueueSaveAndWaitAsync(context.CharacterId, context.Player, CancellationToken.None);
            Console.WriteLine($"[Session {connectionId}] Saved character: {context.Player.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Session {connectionId}] Error during graceful disconnect: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        // Get client IP address
        var clientEndPoint = client.Client.RemoteEndPoint as System.Net.IPEndPoint;
        var clientIp = clientEndPoint?.Address.ToString() ?? "unknown";

        await using var networkStream = client.GetStream();
        var session = new TelnetSession(networkStream);
        var connectionId = Interlocked.Increment(ref _nextConnectionId);

        ConnectionContext? context = null;
        var sessionData = new SessionData { IpAddress = clientIp };

        // Create a scope for this client connection to get scoped services
        await using var scope = _serviceProvider.CreateAsyncScope();
        var characterRepository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();

        try
        {
            Console.WriteLine($"[Session {connectionId}] Client connected from {clientIp}");
            
            // Check if IP is banned before allowing connection
            if (_ipBanService.IsBanned(clientIp))
            {
                var banTime = _ipBanService.GetRemainingBanTime(clientIp);
                var banMessage = banTime.HasValue 
                    ? $"Your IP address is banned. Please try again in {banTime.Value.TotalMinutes:F0} minutes.\n"
                    : "Your IP address is banned.\n";
                
                await session.SendLineAsync(banMessage, cancellationToken);
                return;
            }

            // Send welcome message
            await session.SendLineAsync(_promptCatalog.GetWelcomeMessage(), cancellationToken);
            await session.SendLineAsync(_promptCatalog.GetAccountNamePrompt(), cancellationToken);

            // Authentication and character selection loop
            while (!cancellationToken.IsCancellationRequested && sessionData.State != ConnectionState.Playing)
            {
                var line = await session.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    return; // Client disconnected
                }

                var (nextState, message, updatedSession) = await _authHandler.ProcessInputAsync(
                    line, 
                    sessionData, 
                    cancellationToken);

                sessionData = updatedSession;
                sessionData.State = nextState;

                if (!string.IsNullOrEmpty(message))
                {
                    await session.SendLineAsync(message, cancellationToken);
                }

                // Handle close state
                if (nextState == ConnectionState.Close)
                {
                    return;
                }

                // If we've reached Playing state, break out of auth loop
                if (nextState == ConnectionState.Playing)
                {
                    break;
                }
            }

            // Load the selected character from database and create player state
            if (sessionData.SelectedCharacterId == null)
            {
                await session.SendLineAsync("Error: No character selected.", cancellationToken);
                return;
            }

            var character = await characterRepository.GetByIdAsync(sessionData.SelectedCharacterId.Value, cancellationToken);
            if (character == null)
            {
                await session.SendLineAsync("Error: Character not found.", cancellationToken);
                return;
            }

            var player = CharacterMapper.ToPlayerState(character, connectionId, _worldState);

            context = new ConnectionContext(connectionId, session, player, sessionData.SelectedCharacterId.Value);
            _connections[context.Id] = context;
            _connectionRegistry.SetProvider(() => _connections.Values);

            // Auto-execute look command on entry
            var entryCommand = new CommandRequest("look", null, null);
            await _commandRouter.HandleAsync(entryCommand, context, cancellationToken);

            // Main game loop
            var dispatcher = new CommandParser();
            while (!cancellationToken.IsCancellationRequested)
            {
                var gameLine = await context.Session.ReadLineAsync(cancellationToken);
                if (gameLine is null)
                {
                    break;
                }

                var command = dispatcher.Parse(gameLine);
                var outcome = await _commandRouter.HandleAsync(command, context, cancellationToken);
                if (outcome == CommandOutcome.Disconnect)
                {
                    return;
                }

                if (outcome == CommandOutcome.Unknown)
                {
                    await context.Session.SendLineAsync(_catalog.GetUnknownCommandMessage(), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Session {connectionId}] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            // Save character state to database using the save queue
            // The queue automatically deduplicates saves within 2 seconds
            if (context?.Player is not null && sessionData.SelectedCharacterId.HasValue)
            {
                // Queue the save (fire-and-forget, don't wait)
                await _saveQueue.QueueSaveAsync(sessionData.SelectedCharacterId.Value, context.Player, cancellationToken);
            }
            
            if (context is not null)
            {
                _connections.TryRemove(context.Id, out _);
            }

            client.Close();
        }
    }
}

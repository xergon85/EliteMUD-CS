using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EliteMud.Application;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server;

internal sealed class TelnetServer
{
    private readonly TcpListener _listener;
    private readonly TelnetCommandHandler _commandHandler;
    private readonly ConcurrentDictionary<int, ConnectionContext> _connections = new();
    private int _nextConnectionId;

    public TelnetServer(IPAddress address, int port, IWorldState worldState, IScriptEngine scriptEngine)
    {
        _listener = new TcpListener(address, port);
        _commandHandler = new TelnetCommandHandler(
            worldState,
            scriptEngine,
            () => _connections.Values);
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
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var networkStream = client.GetStream();
        var session = new TelnetSession(networkStream);
        var connectionId = Interlocked.Increment(ref _nextConnectionId);

        ConnectionContext? context = null;

        try
        {
            await session.SendLineAsync("Welcome to EliteMUD (rewrite in progress).", cancellationToken);
            var name = await PromptForNameAsync(session, cancellationToken);
            if (name is null)
            {
                return;
            }

            var player = new PlayerState(connectionId, name, 1);
            context = new ConnectionContext(connectionId, session, player);
            _connections[context.Id] = context;

            var entryCommand = new CommandRequest(CommandKind.Look, null, null);
            await _commandHandler.HandleAsync(entryCommand, context, cancellationToken);

            var dispatcher = new CommandDispatcher();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await context.Session.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                var command = dispatcher.Dispatch(line);
                var outcome = await _commandHandler.HandleAsync(command, context, cancellationToken);
                if (outcome == CommandOutcome.Disconnect)
                {
                    return;
                }

                if (outcome == CommandOutcome.Unknown)
                {
                    await context.Session.SendLineAsync(
                        "Unknown command. Try 'look', 'who', 'say', 'zreset', 'north', or 'go north'.",
                        cancellationToken);
                }
            }
        }
        finally
        {
            if (context is not null)
            {
                _connections.TryRemove(context.Id, out _);
            }

            client.Close();
        }
    }

    private async ValueTask<string?> PromptForNameAsync(TelnetSession session,
        CancellationToken cancellationToken)
    {
        var validator = new PlayerNameValidator();
        while (!cancellationToken.IsCancellationRequested)
        {
            await session.SendLineAsync("Enter your name:", cancellationToken);
            var line = await session.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return null;
            }

            var name = line.Trim();
            if (validator.IsValid(name))
            {
                return name;
            }

            await session.SendLineAsync("Names must be 3-16 letters.", cancellationToken);
        }

        return null;
    }
}

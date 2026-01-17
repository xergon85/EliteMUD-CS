using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.Session.Login;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Commands.Look;
using EliteMud.Server.Commands.Move;
using EliteMud.Server.Commands.NoOp;
using EliteMud.Server.Commands.Quit;
using EliteMud.Server.Commands.ResetZone;
using EliteMud.Server.Commands.Say;
using EliteMud.Server.Commands.Shared;
using EliteMud.Server.Commands.Who;

namespace EliteMud.Server;

internal sealed class TelnetServer
{
    private readonly TcpListener _listener;
    private readonly CommandCatalog _catalog;
    private readonly PromptCatalog _promptCatalog;
    private readonly CommandRouter _commandRouter;
    private readonly ConcurrentDictionary<int, ConnectionContext> _connections = new();
    private int _nextConnectionId;

    public TelnetServer(IPAddress address, int port, IWorldState worldState, IScriptEngine scriptEngine)
    {
        _listener = new TcpListener(address, port);
        _catalog = new CommandCatalog();
        _promptCatalog = new PromptCatalog();
        var services = new TelnetCommandServices(
            worldState,
            scriptEngine,
            _catalog,
            () => _connections.Values);
        _commandRouter = new CommandRouter(new ICommandHandler[]
        {
            new NoOpCommandHandler(),
            new QuitCommandHandler(),
            new LookCommandHandler(services),
            new WhoCommandHandler(services),
            new ResetZoneCommandHandler(services),
            new SayCommandHandler(services),
            new MoveCommandHandler(services)
        });
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
            await session.SendLineAsync(_promptCatalog.GetWelcomeMessage(), cancellationToken);
            var name = await PromptForNameAsync(session, cancellationToken);
            if (name is null)
            {
                return;
            }

            var player = new PlayerState(connectionId, name, 1);
            context = new ConnectionContext(connectionId, session, player);
            _connections[context.Id] = context;

            var entryCommand = new CommandRequest(CommandKind.Look, null, null);
            await _commandRouter.HandleAsync(entryCommand, context, cancellationToken);

            var dispatcher = new CommandParser();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await context.Session.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                var command = dispatcher.Parse(line);
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
        var loginHandler = new LoginHandler();
        while (!cancellationToken.IsCancellationRequested)
        {
            await session.SendLineAsync(_promptCatalog.GetNamePrompt(), cancellationToken);
            var line = await session.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return null;
            }

            var result = loginHandler.ValidateName(line);
            if (result.Success)
            {
                return line.Trim();
            }

            await session.SendLineAsync(result.Message, cancellationToken);
        }

        return null;
    }
}

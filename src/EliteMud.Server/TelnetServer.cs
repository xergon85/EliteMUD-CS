using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EliteMud.Application;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server;

internal sealed class TelnetServer : IConnectionDirectory
{
    private readonly TcpListener _listener;
    private readonly IWorldState _worldState;
    private readonly IScriptEngine _scriptEngine;
    private readonly LookHandler _lookHandler;
    private readonly MoveHandler _moveHandler;
    private readonly ResetZoneHandler _resetZoneHandler;
    private readonly SayHandler _sayHandler;
    private readonly WhoHandler _whoHandler;
    private readonly ConcurrentDictionary<int, ConnectionContext> _connections = new();
    private int _nextConnectionId;

    public IReadOnlyList<string> GetPlayerNames()
    {
        return _connections.Values
            .Select(connection => connection.Player.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TelnetServer(IPAddress address, int port, IWorldState worldState, IScriptEngine scriptEngine)
    {
        _listener = new TcpListener(address, port);
        _worldState = worldState;
        _scriptEngine = scriptEngine;
        _lookHandler = new LookHandler(worldState);
        _moveHandler = new MoveHandler(worldState);
        _resetZoneHandler = new ResetZoneHandler(worldState);
        _sayHandler = new SayHandler();
        _whoHandler = new WhoHandler(this);
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

            await ExecuteHookAsync(context, ScriptHook.OnEnterRoom, null, cancellationToken);
            await RenderRoomAsync(context, cancellationToken);

            var dispatcher = new CommandDispatcher();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await context.Session.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                var command = dispatcher.Dispatch(line);
                switch (command.Kind)
                {
                    case CommandKind.None:
                        continue;
                    case CommandKind.Quit:
                        await context.Session.SendLineAsync("Goodbye!", cancellationToken);
                        return;
                    case CommandKind.Look:
                        await RenderRoomAsync(context, cancellationToken);
                        continue;
                    case CommandKind.Who:
                        await ShowWhoAsync(context, cancellationToken);
                        continue;
                    case CommandKind.ResetZone:
                        await ResetZoneAsync(context, command.Argument, cancellationToken);
                        continue;
                    case CommandKind.Say:
                        await SayAsync(context, command.Argument ?? string.Empty, cancellationToken);
                        continue;
                    case CommandKind.Move:
                        if (command.Direction.HasValue)
                        {
                            await MoveAsync(context, command.Direction.Value, cancellationToken);
                            continue;
                        }

                        break;
                    default:
                        break;
                }

                await context.Session.SendLineAsync(
                    "Unknown command. Try 'look', 'who', 'say', 'zreset', 'north', or 'go north'.",
                    cancellationToken);
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

    private async ValueTask ShowWhoAsync(ConnectionContext context, CancellationToken cancellationToken)
    {
        var result = _whoHandler.Handle();
        await context.Session.SendLineAsync("Players:", cancellationToken);
        foreach (var name in result.Names)
        {
            await context.Session.SendLineAsync($" - {name}", cancellationToken);
        }
    }

    private async ValueTask ResetZoneAsync(ConnectionContext context, string? idText,
        CancellationToken cancellationToken)
    {
        int? zoneId = null;
        if (!string.IsNullOrWhiteSpace(idText))
        {
            if (!int.TryParse(idText, out var parsedId))
            {
                await context.Session.SendLineAsync("Usage: zreset [zoneId]", cancellationToken);
                return;
            }

            zoneId = parsedId;
        }

        var result = _resetZoneHandler.Handle(context.Player, zoneId);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        if (result.Success)
        {
            await RenderRoomAsync(context, cancellationToken);
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

    private async ValueTask MoveAsync(ConnectionContext context, Direction direction,
        CancellationToken cancellationToken)
    {
        var result = _moveHandler.Handle(context.Player, direction);
        if (!result.Moved)
        {
            await context.Session.SendLineAsync(result.Message ?? "You cannot go that way.", cancellationToken);
            return;
        }

        await ExecuteHookAsync(context, ScriptHook.OnEnterRoom, null, cancellationToken);
        await RenderRoomAsync(context, cancellationToken);
    }

    private async ValueTask SayAsync(ConnectionContext context, string message, CancellationToken cancellationToken)
    {
        var result = _sayHandler.Handle(context.Player, message);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return;
        }

        await ExecuteHookAsync(context, ScriptHook.OnSay, message, cancellationToken);
        await BroadcastRoomAsync(context, result.BroadcastMessage, cancellationToken);
    }

    private async ValueTask RenderRoomAsync(ConnectionContext context, CancellationToken cancellationToken)
    {
        var view = _lookHandler.Handle(context.Player);
        await context.Session.SendLineAsync(view.Name, cancellationToken);
        await context.Session.SendLineAsync(view.Description, cancellationToken);
        foreach (var line in view.MobLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        await context.Session.SendLineAsync(view.ExitLine, cancellationToken);
        await ExecuteHookAsync(context, ScriptHook.OnLook, null, cancellationToken);
    }

    private async ValueTask ExecuteHookAsync(
        ConnectionContext context,
        ScriptHook hook,
        string? text,
        CancellationToken cancellationToken)
    {
        var room = _worldState.World.GetRoom(context.Player.RoomId);
        var scriptContext = new ScriptContext(context.Player, room, text);
        await _scriptEngine.ExecuteAsync(hook, scriptContext, cancellationToken);
        foreach (var output in scriptContext.Outputs)
        {
            await context.Session.SendLineAsync(output, cancellationToken);
        }
    }

    private async ValueTask BroadcastRoomAsync(ConnectionContext speaker, string message,
        CancellationToken cancellationToken)
    {
        foreach (var connection in _connections.Values)
        {
            if (connection.Id == speaker.Id)
            {
                continue;
            }

            if (connection.Player.RoomId != speaker.Player.RoomId)
            {
                continue;
            }

            await connection.Session.SendLineAsync(message, cancellationToken);
        }
    }

    private static bool TryParseDirection(string input, out Direction direction)
    {
        switch (input.Trim().ToLowerInvariant())
        {
            case "north":
            case "n":
                direction = Direction.North;
                return true;
            case "east":
            case "e":
                direction = Direction.East;
                return true;
            case "south":
            case "s":
                direction = Direction.South;
                return true;
            case "west":
            case "w":
                direction = Direction.West;
                return true;
            case "up":
            case "u":
                direction = Direction.Up;
                return true;
            case "down":
            case "d":
                direction = Direction.Down;
                return true;
            default:
                direction = Direction.North;
                return false;
        }
    }
}

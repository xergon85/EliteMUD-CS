using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server;

internal sealed class TelnetServer
{
    private readonly TcpListener _listener;
    private readonly WorldState _worldState;
    private readonly IScriptEngine _scriptEngine;
    private readonly ConcurrentDictionary<int, ConnectionContext> _connections = new();
    private int _nextConnectionId;

    public TelnetServer(IPAddress address, int port, WorldState worldState, IScriptEngine scriptEngine)
    {
        _listener = new TcpListener(address, port);
        _worldState = worldState;
        _scriptEngine = scriptEngine;
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

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await context.Session.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (line.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    await context.Session.SendLineAsync("Goodbye!", cancellationToken);
                    break;
                }

                if (line.Equals("look", StringComparison.OrdinalIgnoreCase))
                {
                    await RenderRoomAsync(context, cancellationToken);
                    continue;
                }

                if (line.Equals("who", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowWhoAsync(context, cancellationToken);
                    continue;
                }

                if (line.Equals("zreset", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("reset", StringComparison.OrdinalIgnoreCase))
                {
                    await ResetZoneAsync(context, null, cancellationToken);
                    continue;
                }

                if (line.StartsWith("zreset ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("reset ", StringComparison.OrdinalIgnoreCase))
                {
                    var idText = line[(line.IndexOf(' ') + 1)..].Trim();
                    await ResetZoneAsync(context, idText, cancellationToken);
                    continue;
                }

                if (line.StartsWith("say ", StringComparison.OrdinalIgnoreCase))
                {
                    var message = line[4..].Trim();
                    await SayAsync(context, message, cancellationToken);
                    continue;
                }

                if (line.Equals("say", StringComparison.OrdinalIgnoreCase))
                {
                    await context.Session.SendLineAsync("Say what?", cancellationToken);
                    continue;
                }

                if (TryParseDirection(line, out var direction))
                {
                    await MoveAsync(context, direction, cancellationToken);
                    continue;
                }

                if (line.StartsWith("go ", StringComparison.OrdinalIgnoreCase)
                    && TryParseDirection(line[3..], out direction))
                {
                    await MoveAsync(context, direction, cancellationToken);
                    continue;
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
        await context.Session.SendLineAsync("Players:", cancellationToken);
        foreach (var connection in _connections.Values)
        {
            await context.Session.SendLineAsync($" - {connection.Player.Name}", cancellationToken);
        }
    }

    private async ValueTask ResetZoneAsync(ConnectionContext context, string? idText,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(idText))
        {
            if (!int.TryParse(idText, out var zoneId))
            {
                await context.Session.SendLineAsync("Usage: zreset [zoneId]", cancellationToken);
                return;
            }

            if (!_worldState.ResetZone(zoneId))
            {
                await context.Session.SendLineAsync("Zone not found.", cancellationToken);
                return;
            }

            await context.Session.SendLineAsync($"Zone {zoneId} reset.", cancellationToken);
            await RenderRoomAsync(context, cancellationToken);
            return;
        }

        if (!_worldState.ResetZoneForRoom(context.Player.RoomId, out var currentZoneId))
        {
            await context.Session.SendLineAsync("You are not in a zone with resets.", cancellationToken);
            return;
        }

        await context.Session.SendLineAsync($"Zone {currentZoneId} reset.", cancellationToken);
        await RenderRoomAsync(context, cancellationToken);
    }

    private static async ValueTask<string?> PromptForNameAsync(TelnetSession session,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await session.SendLineAsync("Enter your name:", cancellationToken);
            var line = await session.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return null;
            }

            var name = line.Trim();
            if (IsValidName(name))
            {
                return name;
            }

            await session.SendLineAsync("Names must be 3-16 letters.", cancellationToken);
        }

        return null;
    }

    private static bool IsValidName(string name)
    {
        if (name.Length is < 3 or > 16)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsLetter(character))
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask MoveAsync(ConnectionContext context, Direction direction,
        CancellationToken cancellationToken)
    {
        if (!_worldState.World.TryMove(context.Player.RoomId, direction, out var targetRoomId))
        {
            await context.Session.SendLineAsync("You cannot go that way.", cancellationToken);
            return;
        }

        context.Player.RoomId = targetRoomId;
        await ExecuteHookAsync(context, ScriptHook.OnEnterRoom, null, cancellationToken);
        await RenderRoomAsync(context, cancellationToken);
    }

    private async ValueTask SayAsync(ConnectionContext context, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await context.Session.SendLineAsync("Say what?", cancellationToken);
            return;
        }

        await context.Session.SendLineAsync($"You say, '{message}'.", cancellationToken);
        await ExecuteHookAsync(context, ScriptHook.OnSay, message, cancellationToken);
        await BroadcastRoomAsync(context, $"{context.Player.Name} says, '{message}'.", cancellationToken);
    }

    private async ValueTask RenderRoomAsync(ConnectionContext context, CancellationToken cancellationToken)
    {
        var room = _worldState.World.GetRoom(context.Player.RoomId);
        await context.Session.SendLineAsync(room.Name, cancellationToken);
        await context.Session.SendLineAsync(room.Description, cancellationToken);
        foreach (var mob in _worldState.GetMobsInRoom(room.Id))
        {
            var line = string.IsNullOrWhiteSpace(mob.Definition.LongDescription)
                ? mob.Definition.ShortDescription
                : mob.Definition.LongDescription.TrimEnd();
            if (!string.IsNullOrWhiteSpace(line))
            {
                await context.Session.SendLineAsync(line, cancellationToken);
            }
        }

        await context.Session.SendLineAsync(BuildExitLine(room), cancellationToken);
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

    private static string BuildExitLine(RoomDefinition room)
    {
        if (room.Exits.Count == 0)
        {
            return "Exits: none.";
        }

        var names = new List<string>(room.Exits.Count);
        foreach (var exit in room.Exits)
        {
            names.Add(exit.Direction.ToString().ToLowerInvariant());
        }

        return $"Exits: {string.Join(", ", names)}.";
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

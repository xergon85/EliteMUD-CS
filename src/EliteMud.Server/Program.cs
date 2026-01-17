using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server;

internal static class Program
{
    private const int DefaultPort = 7500;

    public static async Task Main(string[] args)
    {
        var port = TryParsePort(args) ?? DefaultPort;
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var contentRoot = ResolveContentRoot();
        var world = ContentLoader.LoadWorld(contentRoot);
        if (world is null)
        {
            Console.WriteLine($"Content not found under {contentRoot}; using bootstrap world.");
            world = BuildBootstrapWorld();
        }
        else
        {
            Console.WriteLine($"Loaded {world.Rooms.Count} rooms from {contentRoot}.");
        }

        var scripts = ContentLoader.LoadScripts(contentRoot);
        if (scripts.Count == 0)
        {
            Console.WriteLine($"No scripts found under {contentRoot}; using bootstrap scripts.");
            scripts = BuildBootstrapScriptDefinitions();
        }
        else
        {
            Console.WriteLine($"Loaded {scripts.Count} scripts from {contentRoot}.");
        }

        var mobs = ContentLoader.LoadMobs(contentRoot);
        var objects = ContentLoader.LoadObjects(contentRoot);
        var zones = ContentLoader.LoadZones(contentRoot);
        Console.WriteLine($"Loaded {mobs.Count} mobs, {objects.Count} objects, {zones.Count} zones from {contentRoot}.");

        var worldState = BuildWorldState(world, mobs, zones);
        var scriptEngine = BuildScriptEngine(scripts);
        var server = new TelnetServer(IPAddress.Any, port, worldState, scriptEngine);
        Console.WriteLine($"EliteMUD Telnet server listening on {port}.");
        await server.RunAsync(cancellationTokenSource.Token);
    }

    private static int? TryParsePort(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        return int.TryParse(args[0], out var port) ? port : null;
    }

    private static WorldDefinition BuildBootstrapWorld()
    {
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new RoomDefinition(
                1,
                "The Entry Hall",
                "A simple stone hall with torchlight flickering along the walls.",
                new List<ExitDefinition>
                {
                    new(Direction.North, 2)
                }),
            [2] = new RoomDefinition(
                2,
                "The Training Yard",
                "An open yard with worn practice dummies and sand underfoot.",
                new List<ExitDefinition>
                {
                    new(Direction.South, 1)
                })
        };

        return new WorldDefinition(rooms);
    }

    private static string ResolveContentRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "content");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, "content");
    }

    private static IScriptEngine BuildScriptEngine(IReadOnlyList<ScriptDefinition> scripts)
    {
        var engine = new LuaScriptEngine();
        foreach (var script in scripts)
        {
            engine.RegisterAsync(script, CancellationToken.None);
        }

        return engine;
    }

    private static IReadOnlyList<ScriptDefinition> BuildBootstrapScriptDefinitions()
    {
        return new List<ScriptDefinition>
        {
            new(
                "entry-hall-look",
                "OnLook",
                "emit('Dust motes drift lazily in the torchlight.')",
                1),
            new(
                "training-yard-enter",
                "OnEnterRoom",
                "emit('You hear distant clangs of steel.')",
                2),
            new(
                "entry-hall-say",
                "OnSay",
                "if text:find('hello') then emit('An unseen voice whispers back.') end",
                1)
        };
    }

    private static WorldState BuildWorldState(
        WorldDefinition world,
        IReadOnlyList<MobDefinition> mobs,
        IReadOnlyList<ZoneDefinition> zones)
    {
        var mobIndex = new Dictionary<int, MobDefinition>();
        foreach (var mob in mobs)
        {
            mobIndex[mob.Id] = mob;
        }

        var roomMobs = new Dictionary<int, List<MobInstance>>();
        foreach (var roomId in world.Rooms.Keys)
        {
            roomMobs[roomId] = new List<MobInstance>();
        }

        var nextMobInstanceId = 1;
        foreach (var zone in zones)
        {
            foreach (var reset in zone.ResetCommands)
            {
                if (!string.Equals(reset.Type, "LoadMob", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!reset.MobId.HasValue || !reset.RoomId.HasValue)
                {
                    continue;
                }

                if (!mobIndex.TryGetValue(reset.MobId.Value, out var mobDefinition))
                {
                    continue;
                }

                if (!roomMobs.TryGetValue(reset.RoomId.Value, out var list))
                {
                    list = new List<MobInstance>();
                    roomMobs[reset.RoomId.Value] = list;
                }

                var desiredCount = Math.Max(1, reset.MaxExisting ?? 1);
                var existing = 0;
                foreach (var instance in list)
                {
                    if (instance.Definition.Id == mobDefinition.Id)
                    {
                        existing++;
                    }
                }

                var toSpawn = desiredCount - existing;
                for (var i = 0; i < toSpawn; i++)
                {
                    list.Add(new MobInstance(nextMobInstanceId++, mobDefinition));
                }
            }
        }

        var worldState = new WorldState(world, mobIndex, roomMobs, zones);
        worldState.ResetAllZones();
        return worldState;
    }
}


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

                await context.Session.SendLineAsync("Unknown command. Try 'look', 'who', 'say', 'zreset', 'north', or 'go north'.", cancellationToken);
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

    private async ValueTask ResetZoneAsync(ConnectionContext context, string? idText, CancellationToken cancellationToken)
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

    private async ValueTask<string?> PromptForNameAsync(TelnetSession session, CancellationToken cancellationToken)

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

    private async ValueTask MoveAsync(ConnectionContext context, Direction direction, CancellationToken cancellationToken)
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

    private async ValueTask BroadcastRoomAsync(ConnectionContext speaker, string message, CancellationToken cancellationToken)
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

internal sealed class ConnectionContext
{
    public ConnectionContext(int id, TelnetSession session, PlayerState player)
    {
        Id = id;
        Session = session;
        Player = player;
    }

    public int Id { get; }

    public TelnetSession Session { get; }

    public PlayerState Player { get; }
}

internal sealed class WorldState
{
    private readonly Dictionary<int, MobDefinition> _mobDefinitions;
    private readonly Dictionary<int, List<MobInstance>> _roomMobs;
    private readonly IReadOnlyList<ZoneDefinition> _zones;
    private int _nextMobInstanceId;

    public WorldState(
        WorldDefinition world,
        Dictionary<int, MobDefinition> mobDefinitions,
        Dictionary<int, List<MobInstance>> roomMobs,
        IReadOnlyList<ZoneDefinition> zones)
    {
        World = world;
        _mobDefinitions = mobDefinitions;
        _roomMobs = roomMobs;
        _zones = zones;
    }

    public WorldDefinition World { get; }

    public IReadOnlyDictionary<int, MobDefinition> MobDefinitions => _mobDefinitions;

    public IReadOnlyList<MobInstance> GetMobsInRoom(int roomId)
    {
        return _roomMobs.TryGetValue(roomId, out var mobs)
            ? mobs
            : Array.Empty<MobInstance>();
    }

    public void ResetAllZones()
    {
        foreach (var zone in _zones)
        {
            ResetZone(zone.Id);
        }
    }

    public bool ResetZoneForRoom(int roomId, out int zoneId)
    {
        foreach (var zone in _zones)
        {
            if (roomId >= zone.RoomRange.Min && roomId <= zone.RoomRange.Max)
            {
                zoneId = zone.Id;
                return ResetZone(zone.Id);
            }
        }

        zoneId = 0;
        return false;
    }

    public bool ResetZone(int zoneId)
    {
        var zone = FindZone(zoneId);
        if (zone is null)
        {
            return false;
        }

        ClearZoneRooms(zone);
        ApplyZoneResets(zone);
        return true;
    }

    private ZoneDefinition? FindZone(int zoneId)
    {
        foreach (var zone in _zones)
        {
            if (zone.Id == zoneId)
            {
                return zone;
            }
        }

        return null;
    }

    private void ClearZoneRooms(ZoneDefinition zone)
    {
        foreach (var roomId in _roomMobs.Keys)
        {
            if (roomId < zone.RoomRange.Min || roomId > zone.RoomRange.Max)
            {
                continue;
            }

            _roomMobs[roomId].Clear();
        }
    }

    private void ApplyZoneResets(ZoneDefinition zone)
    {
        foreach (var reset in zone.ResetCommands)
        {
            if (!string.Equals(reset.Type, "LoadMob", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!reset.MobId.HasValue || !reset.RoomId.HasValue)
            {
                continue;
            }

            if (!_mobDefinitions.TryGetValue(reset.MobId.Value, out var mobDefinition))
            {
                continue;
            }

            if (!_roomMobs.TryGetValue(reset.RoomId.Value, out var list))
            {
                list = new List<MobInstance>();
                _roomMobs[reset.RoomId.Value] = list;
            }

            var desiredCount = Math.Max(1, reset.MaxExisting ?? 1);
            var existing = 0;
            foreach (var instance in list)
            {
                if (instance.Definition.Id == mobDefinition.Id)
                {
                    existing++;
                }
            }

            var toSpawn = desiredCount - existing;
            for (var i = 0; i < toSpawn; i++)
            {
                list.Add(new MobInstance(_nextMobInstanceId++, mobDefinition));
            }
        }
    }
}

internal sealed record MobInstance(int InstanceId, MobDefinition Definition);

internal sealed class TelnetSession
{
    private const byte TelnetIac = 255;
    private const byte TelnetCommandLength = 3;

    private readonly NetworkStream _stream;
    private readonly Encoding _encoding = Encoding.ASCII;

    public TelnetSession(NetworkStream stream)
    {
        _stream = stream;
    }

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            var builder = new StringBuilder();
            while (true)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    return null;
                }

                var cursor = 0;
                while (cursor < bytesRead)
                {
                    var current = buffer[cursor];
                    if (current == TelnetIac)
                    {
                        cursor = SkipTelnetCommand(cursor, bytesRead);
                        continue;
                    }

                    if (current == '\n')
                    {
                        return builder.ToString().TrimEnd('\r');
                    }

                    builder.Append((char)current);
                    cursor++;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask SendLineAsync(string message, CancellationToken cancellationToken)
    {
        var payload = _encoding.GetBytes(message + "\r\n");
        await _stream.WriteAsync(payload, cancellationToken);
    }

    private static int SkipTelnetCommand(int cursor, int bytesRead)
    {
        var next = cursor + TelnetCommandLength;
        return next <= bytesRead ? next : bytesRead;
    }
}

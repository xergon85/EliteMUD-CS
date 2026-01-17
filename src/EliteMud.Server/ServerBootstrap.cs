using System.Net;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server;

internal static class ServerBootstrap
{
    public const int DefaultPort = 7500;

    public static TelnetServer CreateServer(int port)
    {
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
        Console.WriteLine(
            $"Loaded {mobs.Count} mobs, {objects.Count} objects, {zones.Count} zones from {contentRoot}.");

        var worldState = BuildWorldState(world, mobs, zones);
        var scriptEngine = BuildScriptEngine(scripts);
        return new TelnetServer(IPAddress.Any, port, worldState, scriptEngine);
    }

    public static int? TryParsePort(string[] args)
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

        var worldState = new WorldState(world, mobIndex, roomMobs, zones);
        worldState.ResetAllZones();
        return worldState;
    }
}

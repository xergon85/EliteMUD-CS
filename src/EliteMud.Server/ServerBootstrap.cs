using System.Net;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;
using MobInstance = EliteMud.Application.World.MobInstance;

namespace EliteMud.Server;

internal static class ServerBootstrap
{
    public const int DefaultPort = 7500;

    public static TelnetServer CreateServer(int port)
    {
        var contentRoot = ResolveContentRoot();
        
        WorldDefinition? world;
        IReadOnlyList<MobDefinition> mobs;
        IReadOnlyList<ObjectDefinition> objects;
        IReadOnlyList<ZoneDefinition> zones;
        
        // Try loading from zone-grouped files first
        var zonesDirectory = Path.Combine(contentRoot, "..", "zones");
        if (Directory.Exists(zonesDirectory))
        {
            Console.WriteLine($"Found zone-grouped content in {zonesDirectory}");
            (world, mobs, objects, zones) = ContentLoader.LoadFromZoneFiles(zonesDirectory);
            
            if (world is not null)
            {
                return CreateServerFromContent(port, world, mobs, objects, zones, contentRoot);
            }
        }

        // Fall back to old monolithic format
        Console.WriteLine($"Loading from monolithic content in {contentRoot}");
        world = ContentLoader.LoadWorld(contentRoot);
        if (world is null)
        {
            Console.WriteLine($"Content not found under {contentRoot}; using bootstrap world.");
            world = BuildBootstrapWorld();
        }
        else
        {
            Console.WriteLine($"Loaded {world.Rooms.Count} rooms from {contentRoot}.");
        }

        mobs = ContentLoader.LoadMobs(contentRoot);
        objects = ContentLoader.LoadObjects(contentRoot);
        zones = ContentLoader.LoadZones(contentRoot);
        Console.WriteLine(
            $"Loaded {mobs.Count} mobs, {objects.Count} objects, {zones.Count} zones from {contentRoot}.");

        return CreateServerFromContent(port, world, mobs, objects, zones, contentRoot);
    }

    private static TelnetServer CreateServerFromContent(
        int port,
        WorldDefinition world,
        IReadOnlyList<MobDefinition> mobs,
        IReadOnlyList<ObjectDefinition> objects,
        IReadOnlyList<ZoneDefinition> zones,
        string contentRoot)
    {
        var scripts = ContentLoader.LoadScripts(contentRoot);
        if (scripts.Count == 0)
        {
            Console.WriteLine($"No scripts found; using bootstrap scripts.");
            scripts = BuildBootstrapScriptDefinitions();
        }

        var worldState = BuildWorldState(world, mobs, objects, zones);
        var scriptEngine = BuildScriptEngine(scripts);

        var services = new ServiceCollection()
            .AddSingleton<IWorldState>(worldState)
            .AddSingleton(scriptEngine)
            .AddCommandHandlers()
            .BuildServiceProvider();

        var commandRouter = services.GetRequiredService<CommandRouter>();
        var catalog = services.GetRequiredService<CommandCatalog>();
        var promptCatalog = services.GetRequiredService<PromptCatalog>();
        var connectionRegistry = services.GetRequiredService<ConnectionRegistry>();

        return new TelnetServer(IPAddress.Any, port, catalog, promptCatalog, commandRouter, connectionRegistry);
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
        IReadOnlyList<ObjectDefinition> objects,
        IReadOnlyList<ZoneDefinition> zones)
    {
        var mobDefinitions = new Dictionary<int, MobDefinition>();
        foreach (var mob in mobs)
        {
            mobDefinitions[mob.Id] = mob;
        }

        var objectDefinitions = new Dictionary<int, ObjectDefinition>();
        foreach (var obj in objects)
        {
            objectDefinitions[obj.Id] = obj;
        }

        var roomMobs = new Dictionary<int, List<MobInstance>>();
        var roomObjects = new Dictionary<int, List<ObjectInstance>>();
        foreach (var roomId in world.Rooms.Keys)
        {
            roomMobs[roomId] = new List<MobInstance>();
            roomObjects[roomId] = new List<ObjectInstance>();
        }

        var worldState = new WorldState(world, mobDefinitions, objectDefinitions, roomMobs, roomObjects, zones);
        worldState.ResetAllZones();
        return worldState;
    }
}

using System.Net;
using EliteMud.Application.Ai;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Progression;
using EliteMud.Application.Session;
using EliteMud.Application.Session.Authentication;
using EliteMud.Application.Spells;
using EliteMud.Application.World;
using EliteMud.Application.World.Validation;
using EliteMud.Data;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MobInstance = EliteMud.Application.World.MobInstance;

namespace EliteMud.Server;

internal static class ServerBootstrap
{
    public const int DefaultPort = 7500;

    public static (TelnetServer Server, GameTickService TickService) CreateServer(int port)
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
                PrintValidationSummary(world, mobs, objects, zones);
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

    private static (TelnetServer Server, GameTickService TickService) CreateServerFromContent(
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

        var (skillMetadataRegistry, spellMetadataRegistry, formulaEvaluator, skillRegistry, spellRegistry) =
            BuildRules(contentRoot);

        // Fail fast: a broken class/race file or formula would otherwise only show up in the middle of play
        var (rulesContent, loadErrors) = RulesContentLoader.Load(contentRoot);
        var rulesErrors = loadErrors
            .Concat(RulesContentLoader.CheckClassReferences(
                rulesContent, skillMetadataRegistry.GetAll(), spellMetadataRegistry.GetAll()))
            .ToList();
        if (rulesErrors.Count > 0)
        {
            throw new ContentConfigurationException(
                $"{rulesErrors.Count} problem(s) in class/race/progression content:\n  " + string.Join("\n  ", rulesErrors));
        }

        Console.WriteLine(
            $"Rules content: {rulesContent.Classes.Count} classes, {rulesContent.Races.Count} races, " +
            $"{rulesContent.XpCurves.Count} XP curves, {rulesContent.LevelUpProfiles.Count} level-up profiles.");

        var formulaErrors = CheckFormulas(formulaEvaluator, skillRegistry, spellRegistry, rulesContent);
        if (formulaErrors.Count > 0)
        {
            throw new ContentConfigurationException(
                $"{formulaErrors.Count} content formula(s) are broken:\n  " + string.Join("\n  ", formulaErrors));
        }

        var worldState = BuildWorldState(world, mobs, objects, zones);
        var scriptEngine = BuildScriptEngine(scripts);

        // Get database path
        var dbPath = Path.Combine(contentRoot, "..", "elitemud.db");
        var connectionString = $"Data Source={dbPath}";

        var services = new ServiceCollection()
            // World and scripting
            .AddSingleton<IWorldState>(worldState)
            .AddSingleton(scriptEngine)
            .AddSingleton(skillMetadataRegistry)
            .AddSingleton(spellMetadataRegistry)
            .AddSingleton(skillRegistry)
            .AddSingleton(spellRegistry)
            .AddSingleton(formulaEvaluator)
            .AddSingleton(rulesContent)
            .AddSingleton<ExperienceService>()
            .AddSingleton<PlayerProgression>()

            // Database
            .AddDbContext<EliteMudDbContext>(options =>
                options.UseSqlite(connectionString))

            // Repositories
            .AddScoped<IAccountRepository, AccountRepository>()
            .AddScoped<ICharacterRepository, CharacterRepository>()

            // Services
            .AddSingleton<IPasswordService, PasswordServiceAdapter>()
            .AddSingleton<IpBanService>(new IpBanService(banDurationMinutes: 15, maxFailedAttempts: 3))
            .AddSingleton<AuthenticationHandler>()
            .AddSingleton<ActMessageService>()
            .AddSingleton<PathfindingService>()
            .AddSingleton<MobAiService>()
            .AddSingleton<GameTickService>()
            .AddSingleton<CharacterSaveQueue>()

            // Commands and other services
            .AddCommandHandlers()
            .BuildServiceProvider();

        // Ensure database is created and migrated
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EliteMudDbContext>();
            db.Database.Migrate();
            Console.WriteLine($"Database initialized at {dbPath}");
        }

        var commandRouter = services.GetRequiredService<CommandRouter>();
        var catalog = services.GetRequiredService<CommandCatalog>();
        var promptCatalog = services.GetRequiredService<PromptCatalog>();
        var connectionRegistry = services.GetRequiredService<ConnectionRegistry>();
        var authHandler = services.GetRequiredService<AuthenticationHandler>();
        var ipBanService = services.GetRequiredService<IpBanService>();
        var tickService = services.GetRequiredService<GameTickService>();
        var saveQueue = services.GetRequiredService<CharacterSaveQueue>();

        var server = new TelnetServer(IPAddress.Any, port, catalog, promptCatalog, commandRouter, connectionRegistry,
            authHandler, services, ipBanService, worldState, saveQueue);
        return (server, tickService);
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

    /// <summary>
    /// Load the zone files and check them, printing every finding. Returns the number of errors.
    /// Used by "--validate".
    /// </summary>
    public static int ValidateContent()
    {
        var zonesDirectory = Path.Combine(ResolveContentRoot(), "..", "zones");
        var (world, mobs, objects, zones) = ContentLoader.LoadFromZoneFiles(zonesDirectory);
        if (world is null)
        {
            Console.WriteLine($"No world content found in {zonesDirectory}.");
            return 1;
        }

        var findings = Validate(world, mobs, objects, zones);
        Console.WriteLine();
        foreach (var line in ContentValidationReport.Details(findings))
        {
            Console.WriteLine(line);
        }

        var (skillMetadata, spellMetadata, formulaEvaluator, skillRegistry, spellRegistry) = BuildRules(ResolveContentRoot());
        var (rulesContent, loadErrors) = RulesContentLoader.Load(ResolveContentRoot());
        var rulesErrors = loadErrors
            .Concat(RulesContentLoader.CheckClassReferences(rulesContent, skillMetadata.GetAll(), spellMetadata.GetAll()))
            .ToList();
        foreach (var error in rulesErrors)
        {
            Console.WriteLine($"Error\tBroken rules content\t{error}");
        }

        var formulaErrors = CheckFormulas(formulaEvaluator, skillRegistry, spellRegistry, rulesContent);
        foreach (var error in formulaErrors)
        {
            Console.WriteLine($"Error\tBroken formula\t{error.ReplaceLineEndings(" ")}");
        }

        Console.WriteLine();
        Console.WriteLine("Summary:");
        foreach (var line in ContentValidationReport.Summarize(findings))
        {
            Console.WriteLine($"  {line}");
        }

        if (rulesErrors.Count > 0)
        {
            Console.WriteLine($"  Error    {rulesErrors.Count,5}  Broken rules content");
        }

        if (formulaErrors.Count > 0)
        {
            Console.WriteLine($"  Error    {formulaErrors.Count,5}  Broken formula");
        }

        return ContentValidationReport.CountErrors(findings) + rulesErrors.Count + formulaErrors.Count;
    }

    /// <summary>
    /// Load skill and spell metadata and create their handlers. Used by startup and by "--validate".
    /// </summary>
    private static (SkillMetadataRegistry Skills, SpellMetadataRegistry Spells, FormulaEvaluator Formulas,
        Application.Skills.SkillRegistry SkillRegistry, SpellRegistry SpellRegistry) BuildRules(string contentRoot)
    {
        var skillMetadataRegistry = new SkillMetadataRegistry(ContentLoader.LoadSkills(contentRoot));
        var spellMetadataRegistry = new SpellMetadataRegistry(ContentLoader.LoadSpells(contentRoot));
        var formulaEvaluator = new FormulaEvaluator();

        return (
            skillMetadataRegistry,
            spellMetadataRegistry,
            formulaEvaluator,
            new Application.Skills.SkillRegistry(skillMetadataRegistry, formulaEvaluator),
            new SpellRegistry(spellMetadataRegistry, formulaEvaluator));
    }

    /// <summary>
    /// Run every skill, spell and level-up formula once with sample inputs (syntax, unknown variables, result type).
    /// </summary>
    private static IReadOnlyList<string> CheckFormulas(
        FormulaEvaluator formulaEvaluator,
        Application.Skills.SkillRegistry skillRegistry,
        SpellRegistry spellRegistry,
        RulesContent rulesContent)
    {
        var sources = skillRegistry.GetAllActiveSkills().Cast<object>()
            .Concat(skillRegistry.GetAllPassiveSkills())
            .Concat(spellRegistry.GetAllSpells())
            .OfType<IFormulaSource>();

        var slots = sources.SelectMany(source => source.FormulaSlots)
            .Concat(rulesContent.LevelUpProfiles.Values.SelectMany(LevelUpFormulaSlots))
            .Append(new FormulaSlot(
                "progression/rules.maxGainFormula",
                rulesContent.Progression.MaxGainFormula,
                typeof(Application.Formulas.GainCapInputs),
                FormulaResult.Number));
        return FormulaChecker.Check(formulaEvaluator, slots);
    }

    private static IEnumerable<FormulaSlot> LevelUpFormulaSlots(LevelUpProfileDefinition profile)
    {
        var inputs = typeof(Application.Formulas.LevelUpInputs);
        yield return new FormulaSlot($"level-up/{profile.Id}.hpGain", profile.HpGainFormula, inputs, FormulaResult.Number);
        yield return new FormulaSlot($"level-up/{profile.Id}.manaGain", profile.ManaGainFormula, inputs, FormulaResult.Number);
        yield return new FormulaSlot($"level-up/{profile.Id}.moveGain", profile.MoveGainFormula, inputs, FormulaResult.Number);
    }

    // Warning mode: problems are reported, but the server still starts.
    private static void PrintValidationSummary(
        WorldDefinition world,
        IReadOnlyList<MobDefinition> mobs,
        IReadOnlyList<ObjectDefinition> objects,
        IReadOnlyList<ZoneDefinition> zones)
    {
        var findings = Validate(world, mobs, objects, zones);
        if (findings.Count == 0)
        {
            Console.WriteLine("Content check: no problems found.");
            return;
        }

        Console.WriteLine("Content check (run with --validate for details):");
        foreach (var line in ContentValidationReport.Summarize(findings))
        {
            Console.WriteLine($"  {line}");
        }
    }

    private static IReadOnlyList<ContentFinding> Validate(
        WorldDefinition world,
        IReadOnlyList<MobDefinition> mobs,
        IReadOnlyList<ObjectDefinition> objects,
        IReadOnlyList<ZoneDefinition> zones) =>
        ContentValidator.Validate(
            world,
            mobs.GroupBy(mob => mob.Id).ToDictionary(group => group.Key, group => group.First()),
            objects.GroupBy(obj => obj.Id).ToDictionary(group => group.Key, group => group.First()),
            zones,
            ContentLoader.LoadAcceptedFindings(ResolveContentRoot()));

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

using EliteMud.Legacy.Import;

namespace EliteMud.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("EliteMUD Legacy Content Importer");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        if (command == "help" || command == "--help" || command == "-h")
        {
            ShowUsage();
            return 0;
        }

        if (command == "import")
        {
            return await HandleImport(args);
        }
        else if (command == "group")
        {
            return await HandleGroup(args);
        }
        else if (command == "rules")
        {
            return await HandleRules(args);
        }
        else
        {
            Console.Error.WriteLine($"Unknown command: {command}");
            Console.Error.WriteLine();
            ShowUsage();
            return 1;
        }
    }

    private static async Task<int> HandleImport(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Error: import requires <legacy-path> and <output-path>");
            Console.Error.WriteLine();
            ShowUsage();
            return 1;
        }

        var legacyPath = args[1];
        var outputPath = args[2];
        var options = ParseOptions(args.Skip(3).ToArray());

        try
        {
            Console.WriteLine($"Legacy path: {legacyPath}");
            Console.WriteLine($"Output path: {outputPath}");
            Console.WriteLine();

            if (options.Config is not null)
            {
                Console.WriteLine($"Config: {options.Config.Skip.Count} skipped files, active zones [{string.Join(", ", options.Config.ActiveZones)}]");
            }

            Console.WriteLine();

            var importer = new LegacyContentImporter();
            var log = await importer.ImportAsync(legacyPath, outputPath, CancellationToken.None, options);

            Console.WriteLine();
            Console.WriteLine("Import summary:");
            foreach (var line in log.Summarize())
            {
                Console.WriteLine($"  {line}");
            }

            Console.WriteLine($"Details: {Path.Combine(outputPath, "import-log.txt")}");
            Console.WriteLine();
            Console.WriteLine("Import completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException is not null)
            {
                Console.Error.WriteLine($"  {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static async Task<int> HandleGroup(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Error: group requires <input-path> and <output-path>");
            Console.Error.WriteLine();
            ShowUsage();
            return 1;
        }

        var inputPath = args[1];
        var outputPath = args[2];

        try
        {
            Console.WriteLine($"Input path: {inputPath}");
            Console.WriteLine($"Output path: {outputPath}");
            Console.WriteLine();

            await ZoneGrouper.GroupByZoneAsync(inputPath, outputPath);

            Console.WriteLine();
            Console.WriteLine("Grouping completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException is not null)
            {
                Console.Error.WriteLine($"  {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static async Task<int> HandleRules(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Error: rules requires <legacy-path> and <content-path>");
            ShowUsage();
            return 1;
        }

        try
        {
            var config = File.Exists(DefaultConfigPath) ? LegacyImportConfig.Load(DefaultConfigPath) : null;
            var log = await new LegacyRulesImporter().ImportAsync(args[1], args[2], CancellationToken.None, config);
            Console.WriteLine("Rules import summary:");
            foreach (var line in log.Summarize())
            {
                Console.WriteLine($"  {line}");
            }

            Console.WriteLine($"Details: {Path.Combine(args[2], "meta", "legacy-rules-import-log.txt")}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or DirectoryNotFoundException or IOException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private const string DefaultConfigPath = "content/meta/legacy-import.json";

    private static LegacyImportOptions ParseOptions(string[] args)
    {
        var includeRooms = true;
        var includeZones = true;
        var includeMobs = true;
        var includeObjects = true;
        string? configPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--config" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;
                case "--no-rooms":
                    includeRooms = false;
                    break;
                case "--no-zones":
                    includeZones = false;
                    break;
                case "--no-mobs":
                    includeMobs = false;
                    break;
                case "--no-objects":
                    includeObjects = false;
                    break;
                default:
                    Console.WriteLine($"Warning: Unknown option '{arg}' ignored");
                    break;
            }
        }

        // Default: the import decisions checked into the repository
        configPath ??= File.Exists(DefaultConfigPath) ? DefaultConfigPath : null;
        var config = configPath is null ? null : LegacyImportConfig.Load(configPath);

        return new LegacyImportOptions(includeRooms, includeZones, includeMobs, includeObjects, config);
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  elitemud-import import <legacy-path> <output-path> [options]");
        Console.WriteLine("  elitemud-import group <input-path> <output-path>");
        Console.WriteLine("  elitemud-import rules <legacy-path> <content-path>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  import  Import legacy EliteMUD world files to JSON");
        Console.WriteLine("  group   Group imported JSON by zone (one file per zone)");
        Console.WriteLine("  rules   Extract classes, races, XP curves and level-up profiles from the legacy C source");
        Console.WriteLine();
        Console.WriteLine("Arguments (import):");
        Console.WriteLine("  <legacy-path>   Path to legacy EliteMUD root directory (tool will automatically find lib/world subdirectory) or direct path to world directory (containing wld, mob, obj, zon folders)");
        Console.WriteLine("  <output-path>   Path to output JSON content directory");
        Console.WriteLine();
        Console.WriteLine("Arguments (group):");
        Console.WriteLine("  <input-path>    Path to imported JSON content directory");
        Console.WriteLine("  <output-path>   Path to output grouped zone files");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --no-rooms      Skip importing rooms");
        Console.WriteLine("  --no-zones      Skip importing zones");
        Console.WriteLine("  --no-mobs       Skip importing mobs");
        Console.WriteLine("  --no-objects    Skip importing objects");
        Console.WriteLine($"  --config <file> Import decisions (skipped files, active zones). Default: {DefaultConfigPath}");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  elitemud-import import ../EliteMUD ./content");
        Console.WriteLine("  elitemud-import import /path/to/legacy/world ./output --no-mobs");
        Console.WriteLine("  elitemud-import group ./content ./zones");
    }
}

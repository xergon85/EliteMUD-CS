using System.Text.Json;

namespace EliteMud.Legacy.Import;

public sealed class LegacyContentImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async ValueTask ImportAsync(
        string legacyWorldPath,
        string outputContentPath,
        CancellationToken cancellationToken,
        LegacyImportOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(legacyWorldPath))
        {
            throw new ArgumentException("Legacy path is required.", nameof(legacyWorldPath));
        }

        if (string.IsNullOrWhiteSpace(outputContentPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputContentPath));
        }

        var importOptions = options ?? new LegacyImportOptions();
        var worldPath = ResolveWorldPath(legacyWorldPath);

        if (importOptions.IncludeRooms)
        {
            var rooms = LegacyRoomImporter.Load(Path.Combine(worldPath, "wld"), cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "rooms"));
            await WriteAsync(Path.Combine(outputContentPath, "rooms", "rooms.json"), new RoomsFile(rooms), cancellationToken);
        }

        if (importOptions.IncludeZones)
        {
            var zones = LegacyZoneImporter.Load(Path.Combine(worldPath, "zon"), cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "zones"));
            await WriteAsync(Path.Combine(outputContentPath, "zones", "zones.json"), new ZonesFile(zones), cancellationToken);
        }

        if (importOptions.IncludeMobs)
        {
            var mobs = LegacyMobImporter.Load(Path.Combine(worldPath, "mob"), cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "mobs"));
            await WriteAsync(Path.Combine(outputContentPath, "mobs", "mobs.json"), new MobsFile(mobs), cancellationToken);
        }

        if (importOptions.IncludeObjects)
        {
            var objPath = Path.Combine(worldPath, "obj");
            if (!Directory.Exists(objPath))
            {
                objPath = Path.Combine(worldPath, "objects");
            }

            var objects = LegacyObjectImporter.Load(objPath, cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "objects"));
            await WriteAsync(Path.Combine(outputContentPath, "objects", "objects.json"), new ObjectsFile(objects), cancellationToken);
        }
    }

    private static async ValueTask WriteAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
    }

    private static string ResolveWorldPath(string legacyWorldPath)
    {
        var worldPath = legacyWorldPath;
        if (Directory.Exists(Path.Combine(worldPath, "lib", "world")))
        {
            worldPath = Path.Combine(worldPath, "lib", "world");
        }

        if (!Directory.Exists(Path.Combine(worldPath, "wld")))
        {
            throw new DirectoryNotFoundException($"Legacy world directory not found under {legacyWorldPath}.");
        }

        return worldPath;
    }
}

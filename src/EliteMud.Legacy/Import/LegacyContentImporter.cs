using System.Text.Encodings.Web;
using System.Text.Json;

namespace EliteMud.Legacy.Import;

public sealed class LegacyContentImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Import the legacy world files to JSON under <paramref name="outputContentPath"/>.
    /// Returns the log of everything that was skipped, resolved or looked wrong; it is also
    /// written to import-log.txt in the output folder.
    /// </summary>
    public async ValueTask<LegacyImportLog> ImportAsync(
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
        var config = importOptions.Config ?? new LegacyImportConfig();
        var log = new LegacyImportLog();
        var worldPath = ResolveWorldPath(legacyWorldPath);

        List<RoomContent>? rooms = null;
        IReadOnlyList<LegacySourceFile> roomFiles = Array.Empty<LegacySourceFile>();
        if (importOptions.IncludeRooms)
        {
            roomFiles = LegacyWorldFiles.Resolve(Path.Combine(worldPath, "wld"), "wld", config, log);
            rooms = LegacyRoomImporter.Load(roomFiles, log, cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "rooms"));
            await WriteAsync(Path.Combine(outputContentPath, "rooms", "rooms.json"), new RoomsFile(rooms), cancellationToken);
        }

        if (importOptions.IncludeZones)
        {
            var zoneFiles = LegacyWorldFiles.Resolve(Path.Combine(worldPath, "zon"), "zon", config, log);
            var zones = LegacyZoneImporter.Load(zoneFiles, config, log, cancellationToken);
            if (rooms is not null)
            {
                zones.AddRange(CreateZonesForUnzonedRoomFiles(roomFiles, rooms, zones, config, log));
            }

            Directory.CreateDirectory(Path.Combine(outputContentPath, "zones"));
            await WriteAsync(Path.Combine(outputContentPath, "zones", "zones.json"), new ZonesFile(zones), cancellationToken);
        }

        if (importOptions.IncludeMobs)
        {
            var mobFiles = LegacyWorldFiles.Resolve(Path.Combine(worldPath, "mob"), "mob", config, log);
            var mobs = LegacyMobImporter.Load(mobFiles, log, cancellationToken);
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

            var objectFiles = LegacyWorldFiles.Resolve(objPath, "obj", config, log);
            var objects = LegacyObjectImporter.Load(objectFiles, log, cancellationToken);
            Directory.CreateDirectory(Path.Combine(outputContentPath, "objects"));
            await WriteAsync(Path.Combine(outputContentPath, "objects", "objects.json"), new ObjectsFile(objects), cancellationToken);
        }

        Directory.CreateDirectory(outputContentPath);
        await log.SaveAsync(Path.Combine(outputContentPath, "import-log.txt"), cancellationToken);
        return log;
    }

    /// <summary>
    /// A room file the original didn't load may have no zone file at all (e.g. the Wedding Chapel, 6.wld).
    /// Give it an empty inactive zone of the same number so its rooms are kept and grouped together.
    /// </summary>
    private static IEnumerable<ZoneContent> CreateZonesForUnzonedRoomFiles(
        IReadOnlyList<LegacySourceFile> roomFiles,
        IReadOnlyList<RoomContent> rooms,
        IReadOnlyList<ZoneContent> zones,
        LegacyImportConfig config,
        LegacyImportLog log)
    {
        var zoneIds = zones.Select(zone => zone.Id).ToHashSet();
        foreach (var file in roomFiles.Where(file => !file.IsIndexed && file.Number is not null))
        {
            var zoneId = file.Number!.Value;
            var fileRooms = rooms.Where(room => room.SourceFile == file.Name).ToList();
            if (zoneIds.Contains(zoneId) || fileRooms.Count == 0)
            {
                continue;
            }

            var name = fileRooms[0].Name;
            log.Info("Zone created", file.Name, $"no zone file; created zone #{zoneId} \"{name}\" for its {fileRooms.Count} rooms");
            yield return new ZoneContent(
                zoneId,
                name,
                fileRooms.Max(room => room.Id),
                Lifespan: 0,
                LegacyImportLookup.ResetMode(0),
                Array.Empty<ZoneResetCommandContent>(),
                Active: config.ActiveZones.Contains(zoneId),
                file.Name);
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

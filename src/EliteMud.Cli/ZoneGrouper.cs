using System.Text.Encodings.Web;
using System.Text.Json;

namespace EliteMud.Cli;

internal static class ZoneGrouper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task GroupByZoneAsync(string inputPath, string outputPath)
    {
        Console.WriteLine("Loading content files...");

        var zonesJson = await File.ReadAllTextAsync(Path.Combine(inputPath, "zones", "zones.json"));
        var roomsJson = await File.ReadAllTextAsync(Path.Combine(inputPath, "rooms", "rooms.json"));
        var mobsJson = await File.ReadAllTextAsync(Path.Combine(inputPath, "mobs", "mobs.json"));
        var objectsJson = await File.ReadAllTextAsync(Path.Combine(inputPath, "objects", "objects.json"));

        using var zonesDoc = JsonDocument.Parse(zonesJson);
        using var roomsDoc = JsonDocument.Parse(roomsJson);
        using var mobsDoc = JsonDocument.Parse(mobsJson);
        using var objectsDoc = JsonDocument.Parse(objectsJson);

        var zones = zonesDoc.RootElement.GetProperty("zones").EnumerateArray().ToList();
        var rooms = roomsDoc.RootElement.GetProperty("rooms").EnumerateArray().ToList();
        var mobs = mobsDoc.RootElement.GetProperty("mobs").EnumerateArray().ToList();
        var objects = objectsDoc.RootElement.GetProperty("objects").EnumerateArray().ToList();

        Console.WriteLine($"Loaded {zones.Count} zones, {rooms.Count} rooms, {mobs.Count} mobs, {objects.Count} objects");
        Console.WriteLine();

        Directory.CreateDirectory(outputPath);

        var activeRanges = ComputeActiveRanges(zones);
        var roomsByZone = AssignRoomsToZones(zones, rooms, activeRanges);

        foreach (var zone in zones)
        {
            var zoneId = zone.GetProperty("Id").GetInt32();
            var zoneName = zone.GetProperty("Name").GetString() ?? $"Zone_{zoneId}";
            var active = !zone.TryGetProperty("Active", out var activeProp) || activeProp.GetBoolean();

            Console.WriteLine($"Processing Zone {zoneId}: {zoneName}{(active ? "" : " (inactive)")}");

            var zoneRooms = roomsByZone.TryGetValue(zoneId, out var assigned) ? assigned : new List<JsonElement>();
            var roomIds = zoneRooms.Select(r => r.GetProperty("Id").GetInt32()).ToHashSet();

            var zoneMobs = GetMobsForZone(mobs, zone, roomIds);
            var zoneObjects = GetObjectsForZone(objects, zone, roomIds);

            var zoneData = new
            {
                Zone = WithRoomRange(zone, activeRanges, roomIds),
                Rooms = zoneRooms.Select(WithoutSourceFile).ToList(),
                Mobs = zoneMobs.Select(m => JsonSerializer.Deserialize<JsonElement>(m.GetRawText())).ToList(),
                Objects = zoneObjects.Select(o => JsonSerializer.Deserialize<JsonElement>(o.GetRawText())).ToList()
            };

            var safeFileName = SanitizeFileName(zoneName);
            var outputFile = Path.Combine(outputPath, $"zone_{zoneId:D3}_{safeFileName}.json");

            await using var stream = File.Create(outputFile);
            await JsonSerializer.SerializeAsync(stream, zoneData, JsonOptions);

            Console.WriteLine($"  - {zoneRooms.Count} rooms, {zoneMobs.Count} mobs, {zoneObjects.Count} objects");
        }

        Console.WriteLine();
        Console.WriteLine($"Created {zones.Count} zone files in {outputPath}");
    }

    /// <summary>
    /// Decide which zone each room belongs to.
    /// Rooms from a file the original didn't load go to the inactive zone with the same number
    /// (1.wld -> zone 1). All other rooms use the legacy rule: a zone owns the rooms after the
    /// previous zone's top room, up to its own top room (zone_table in db.c).
    /// </summary>
    private static Dictionary<int, List<JsonElement>> AssignRoomsToZones(
        List<JsonElement> zones,
        List<JsonElement> rooms,
        List<(int ZoneId, int Min, int Max)> activeRanges)
    {
        var inactiveZoneByFileNumber = zones
            .Where(zone => zone.TryGetProperty("Active", out var active) && !active.GetBoolean())
            .Select(zone => (Zone: zone, FileNumber: FileNumber(zone)))
            .Where(entry => entry.FileNumber is not null)
            .ToDictionary(entry => entry.FileNumber!.Value, entry => entry.Zone.GetProperty("Id").GetInt32());

        var result = new Dictionary<int, List<JsonElement>>();
        foreach (var room in rooms)
        {
            var roomId = room.GetProperty("Id").GetInt32();
            var zoneId = FileNumber(room) is { } fileNumber && inactiveZoneByFileNumber.TryGetValue(fileNumber, out var inactiveZone)
                ? inactiveZone
                : FindZoneByRange(activeRanges, roomId);

            if (zoneId is null)
            {
                Console.WriteLine($"  Warning: room #{roomId} is outside every zone's range; not grouped");
                continue;
            }

            if (!result.TryGetValue(zoneId.Value, out var list))
            {
                list = new List<JsonElement>();
                result[zoneId.Value] = list;
            }

            list.Add(room);
        }

        return result;
    }

    /// <summary>
    /// Legacy zone ranges: each active zone owns the rooms after the previous zone's top room,
    /// up to its own top room.
    /// </summary>
    private static List<(int ZoneId, int Min, int Max)> ComputeActiveRanges(List<JsonElement> zones)
    {
        var ranges = new List<(int ZoneId, int Min, int Max)>();
        var previousTop = -1;
        foreach (var zone in zones
                     .Where(zone => !zone.TryGetProperty("Active", out var active) || active.GetBoolean())
                     .OrderBy(zone => zone.GetProperty("TopRoomId").GetInt32()))
        {
            var top = zone.GetProperty("TopRoomId").GetInt32();
            ranges.Add((zone.GetProperty("Id").GetInt32(), previousTop + 1, top));
            previousTop = top;
        }

        return ranges;
    }

    /// <summary>
    /// Copy the zone with its RoomRange filled in: the legacy range for active zones,
    /// otherwise the span of the rooms assigned to it. The server uses it to find a room's zone.
    /// </summary>
    private static JsonElement WithRoomRange(
        JsonElement zone,
        List<(int ZoneId, int Min, int Max)> activeRanges,
        HashSet<int> roomIds)
    {
        var zoneId = zone.GetProperty("Id").GetInt32();
        var range = activeRanges.FirstOrDefault(r => r.ZoneId == zoneId);
        var (min, max) = activeRanges.Any(r => r.ZoneId == zoneId)
            ? (range.Min, range.Max)
            : roomIds.Count > 0 ? (roomIds.Min(), roomIds.Max()) : (0, -1);

        var copy = new Dictionary<string, object>();
        foreach (var property in zone.EnumerateObject().Where(property => property.Name != "RoomRange"))
        {
            copy[property.Name] = property.Value.Clone();
        }

        copy["RoomRange"] = new { Min = min, Max = max };
        return JsonSerializer.SerializeToElement(copy);
    }

    private static int? FindZoneByRange(List<(int ZoneId, int Min, int Max)> ranges, int roomId)
    {
        foreach (var (zoneId, min, max) in ranges)
        {
            if (roomId >= min && roomId <= max)
            {
                return zoneId;
            }
        }

        return null;
    }

    /// <summary>The number of the legacy file a record came from ("wld/57.wld" -> 57).</summary>
    private static int? FileNumber(JsonElement element) =>
        element.TryGetProperty("SourceFile", out var source)
        && int.TryParse(Path.GetFileNameWithoutExtension(source.GetString()), out var number)
            ? number
            : null;

    private static JsonElement WithoutSourceFile(JsonElement room)
    {
        var copy = new Dictionary<string, JsonElement>();
        foreach (var property in room.EnumerateObject().Where(property => property.Name != "SourceFile"))
        {
            copy[property.Name] = property.Value.Clone();
        }

        return JsonSerializer.SerializeToElement(copy);
    }

    private static List<JsonElement> GetMobsForZone(List<JsonElement> mobs, JsonElement zone, HashSet<int> roomIds)
    {
        var zoneMobs = new HashSet<int>();

        if (zone.TryGetProperty("ResetCommands", out var resets))
        {
            foreach (var reset in resets.EnumerateArray())
            {
                var command = reset.GetProperty("Command").GetString();
                if (command == "M" && reset.TryGetProperty("Arg1", out var mobIdProp))
                {
                    zoneMobs.Add(mobIdProp.GetInt32());
                }
            }
        }

        return mobs.Where(m => zoneMobs.Contains(m.GetProperty("Id").GetInt32())).ToList();
    }

    private static List<JsonElement> GetObjectsForZone(List<JsonElement> objects, JsonElement zone, HashSet<int> roomIds)
    {
        var zoneObjects = new HashSet<int>();

        if (zone.TryGetProperty("ResetCommands", out var resets))
        {
            foreach (var reset in resets.EnumerateArray())
            {
                var command = reset.GetProperty("Command").GetString();
                if ((command == "O" || command == "P" || command == "G" || command == "E") 
                    && reset.TryGetProperty("Arg1", out var objIdProp))
                {
                    zoneObjects.Add(objIdProp.GetInt32());
                }
            }
        }

        return objects.Where(o => zoneObjects.Contains(o.GetProperty("Id").GetInt32())).ToList();
    }

    private static string SanitizeFileName(string name)
    {
        // Remove leading/trailing whitespace and normalize internal whitespace
        var cleaned = name.Trim();
        cleaned = string.Join(" ", cleaned.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        
        // Replace invalid filename characters with underscore
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            cleaned = cleaned.Replace(c, '_');
        }
        
        // Limit length
        return cleaned.Length > 50 ? cleaned.Substring(0, 50) : cleaned;
    }
}

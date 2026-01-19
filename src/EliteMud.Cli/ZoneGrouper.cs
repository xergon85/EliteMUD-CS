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

        foreach (var zone in zones)
        {
            var zoneId = zone.GetProperty("Id").GetInt32();
            var zoneName = zone.GetProperty("Name").GetString() ?? $"Zone_{zoneId}";
            var topRoomId = zone.GetProperty("TopRoomId").GetInt32();

            var minRoomId = (zoneId * 100);
            var maxRoomId = topRoomId;

            Console.WriteLine($"Processing Zone {zoneId}: {zoneName} (rooms {minRoomId}-{maxRoomId})");

            var zoneRooms = rooms.Where(r => 
            {
                var roomId = r.GetProperty("Id").GetInt32();
                return roomId >= minRoomId && roomId <= maxRoomId;
            }).ToList();

            var roomIds = zoneRooms.Select(r => r.GetProperty("Id").GetInt32()).ToHashSet();

            var zoneMobs = GetMobsForZone(mobs, zone, roomIds);
            var zoneObjects = GetObjectsForZone(objects, zone, roomIds);

            var zoneData = new
            {
                Zone = JsonSerializer.Deserialize<JsonElement>(zone.GetRawText()),
                Rooms = zoneRooms.Select(r => JsonSerializer.Deserialize<JsonElement>(r.GetRawText())).ToList(),
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
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}

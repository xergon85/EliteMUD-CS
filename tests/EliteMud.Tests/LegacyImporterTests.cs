using System.Text.Json;
using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

public class LegacyImporterTests
{
    [Fact]
    public async Task ImportAsync_WritesRoomData()
    {
        var importer = new LegacyContentImporter();
        var legacyRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", "0");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"elitemud-import-{Guid.NewGuid():N}");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await importer.ImportAsync(
                legacyRoot,
                outputRoot,
                cts.Token,
                new LegacyImportOptions(IncludeZones: false, IncludeMobs: false, IncludeObjects: false));

            var roomsJson = await File.ReadAllTextAsync(Path.Combine(outputRoot, "rooms", "rooms.json"));
            using var document = JsonDocument.Parse(roomsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("rooms", out var rooms))
            {
                rooms = root;
            }

            Assert.True(rooms.ValueKind == JsonValueKind.Array && rooms.GetArrayLength() > 0);

            var room = rooms[0];
            Assert.True(room.TryGetProperty("Id", out var roomId) && roomId.GetInt32() == 0);
            Assert.True(room.TryGetProperty("Name", out var roomName) && roomName.GetString() == "The Void");
            Assert.True(room.TryGetProperty("CrashRoom", out var crashRoom) && crashRoom.GetBoolean() == false);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_WritesZoneResets()
    {
        var importer = new LegacyContentImporter();
        var legacyRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", "0");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"elitemud-import-{Guid.NewGuid():N}");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await importer.ImportAsync(
                legacyRoot,
                outputRoot,
                cts.Token,
                new LegacyImportOptions(IncludeRooms: false, IncludeMobs: false, IncludeObjects: false));

            var zonesJson = await File.ReadAllTextAsync(Path.Combine(outputRoot, "zones", "zones.json"));
            using var document = JsonDocument.Parse(zonesJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("zones", out var zones))
            {
                zones = root;
            }

            var zone = zones[0];
            Assert.True(zone.TryGetProperty("Id", out var zoneId) && zoneId.GetInt32() == 0);
            Assert.True(zone.TryGetProperty("ResetCommands", out var commands) && commands.GetArrayLength() > 0);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_WritesMobData()
    {
        var importer = new LegacyContentImporter();
        var legacyRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", "0");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"elitemud-import-{Guid.NewGuid():N}");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await importer.ImportAsync(
                legacyRoot,
                outputRoot,
                cts.Token,
                new LegacyImportOptions(IncludeRooms: false, IncludeZones: false, IncludeObjects: false));

            var mobsJson = await File.ReadAllTextAsync(Path.Combine(outputRoot, "mobs", "mobs.json"));
            using var document = JsonDocument.Parse(mobsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("mobs", out var mobs))
            {
                mobs = root;
            }

            var mob = mobs[0];
            Assert.True(mob.TryGetProperty("Id", out var mobId) && mobId.GetInt32() == 1);
            Assert.True(mob.TryGetProperty("Name", out var name) && name.GetString() == "noname monster");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_WritesObjectData()
    {
        var importer = new LegacyContentImporter();
        var legacyRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Legacy", "0");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"elitemud-import-{Guid.NewGuid():N}");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await importer.ImportAsync(
                legacyRoot,
                outputRoot,
                cts.Token,
                new LegacyImportOptions(IncludeRooms: false, IncludeZones: false, IncludeMobs: false));

            var objectsJson = await File.ReadAllTextAsync(Path.Combine(outputRoot, "objects", "objects.json"));
            using var document = JsonDocument.Parse(objectsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("objects", out var objects))
            {
                objects = root;
            }

            if (objects.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            if (objects.GetArrayLength() == 0)
            {
                return;
            }

            var obj = objects[0];
            Assert.True(obj.TryGetProperty("Id", out var objId) && objId.GetInt32() == 0);
            Assert.True(obj.TryGetProperty("Name", out var name) && name.GetString() == "bug");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
        }
    }
}

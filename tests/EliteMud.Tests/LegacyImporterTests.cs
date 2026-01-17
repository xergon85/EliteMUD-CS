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
            await importer.ImportAsync(
                legacyRoot,
                outputRoot,
                CancellationToken.None,
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
            await importer.ImportAsync(
                legacyRoot,
                outputRoot,
                CancellationToken.None,
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
}

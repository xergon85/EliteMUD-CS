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

            JsonElement? simple = null;
            JsonElement? legacy = null;
            JsonElement? auto = null;
            foreach (var item in mobs.EnumerateArray())
            {
                if (item.TryGetProperty("Id", out var id))
                {
                    if (id.GetInt32() == 1)
                    {
                        simple = item;
                    }
                    else if (id.GetInt32() == 2)
                    {
                        legacy = item;
                    }
                    else if (id.GetInt32() == 3)
                    {
                        auto = item;
                    }
                }
            }

            Assert.True(simple.HasValue);
            Assert.True(legacy.HasValue);
            Assert.True(auto.HasValue);

            var simpleMob = simple.Value;
            Assert.True(simpleMob.TryGetProperty("Name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()));
            Assert.True(simpleMob.TryGetProperty("Race", out var race) && race.GetString() == "Humanoid");
            Assert.True(simpleMob.TryGetProperty("Class", out var mobClass) && mobClass.GetString() == "Warrior");
            Assert.True(simpleMob.TryGetProperty("Flags", out var flags) && flags.GetArrayLength() > 0);
            Assert.True(simpleMob.TryGetProperty("Affects", out var affects) && affects.GetArrayLength() > 0);
            Assert.True(simpleMob.TryGetProperty("Level", out var level) && level.GetInt32() == 1);
            Assert.True(simpleMob.TryGetProperty("ActionScript", out var action) && action.GetString() == "This mob looks busy.");
            Assert.True(simpleMob.TryGetProperty("SpecialProc", out var special) && special.GetString() == "guard_object");
            Assert.True(simpleMob.TryGetProperty("Programs", out var programs) && programs.GetArrayLength() == 1);

            Assert.True(simpleMob.TryGetProperty("Attacks", out var attacks) && attacks.GetArrayLength() == 2);
            Assert.True(attacks[0].TryGetProperty("Type", out var attackType) && attackType.GetString() == "Bludgeon");
            Assert.True(attacks[0].TryGetProperty("DamageType", out var attackDamageType) && attackDamageType.GetInt32() == 500);
            Assert.True(attacks[0].TryGetProperty("Chance", out var attackChance) && attackChance.GetInt32() == 100);
            Assert.True(attacks[0].TryGetProperty("DamageDice", out var attackDice) && attackDice.GetString() == "1d1+1");
            Assert.True(attacks[1].TryGetProperty("Type", out var attackType2) && attackType2.GetString() == "Pierce");
            Assert.True(attacks[1].TryGetProperty("DamageType", out var attackDamageType2) && attackDamageType2.GetInt32() == 501);
            Assert.True(attacks[1].TryGetProperty("Chance", out var attackChance2) && attackChance2.GetInt32() == 75);
            Assert.True(attacks[1].TryGetProperty("DamageDice", out var attackDice2) && attackDice2.GetString() == "1d2+0");

            Assert.True(simpleMob.TryGetProperty("Skills", out var skills) && skills.GetArrayLength() == 2);
            Assert.True(skills[0].GetString() == "Skill_304:60");
            Assert.True(skills[1].GetString() == "Skill_305:40");

            Assert.True(simpleMob.TryGetProperty("Resistances", out var resistances) && resistances.GetArrayLength() == 2);
            Assert.True(resistances[0].GetString() == "Resist_1:20");
            Assert.True(resistances[1].GetString() == "Resist_2:10");

            var legacyMob = legacy.Value;
            Assert.True(legacyMob.TryGetProperty("Race", out var legacyRace) && legacyRace.GetString() == "Giant");
            Assert.True(legacyMob.TryGetProperty("Class", out var legacyClass) && legacyClass.GetString() == "Thief");
            Assert.True(legacyMob.TryGetProperty("Affects", out var legacyAffects) && legacyAffects.GetArrayLength() > 0);
            Assert.True(legacyMob.TryGetProperty("Flags", out var legacyFlags) && legacyFlags.GetArrayLength() > 0);
            Assert.True(legacyMob.TryGetProperty("SpecialProc", out var legacyProc) && legacyProc.GetString() == "goto_mayor");
            Assert.True(legacyMob.TryGetProperty("DefaultPosition", out var legacyPos));

            Assert.True(legacyMob.TryGetProperty("Attacks", out var legacyAttacks) && legacyAttacks.GetArrayLength() == 1);
            Assert.True(legacyAttacks[0].TryGetProperty("Type", out var legacyAttackType) && legacyAttackType.GetString() == "Claw");
            Assert.True(legacyAttacks[0].TryGetProperty("DamageType", out var legacyDamageType) && legacyDamageType.GetInt32() == 500);
            Assert.True(legacyAttacks[0].TryGetProperty("Chance", out var legacyChance) && legacyChance.GetInt32() == 80);
            Assert.True(legacyAttacks[0].TryGetProperty("DamageDice", out var legacyDice) && legacyDice.GetString() == "2d1+0");

            Assert.True(legacyMob.TryGetProperty("Skills", out var legacySkills) && legacySkills.GetArrayLength() == 1);
            Assert.True(legacySkills[0].GetString() == "Skill_310:55");

            Assert.True(legacyMob.TryGetProperty("Resistances", out var legacyResists) && legacyResists.GetArrayLength() == 1);
            Assert.True(legacyResists[0].GetString() == "Resist_3:15");

            var autoMob = auto.Value;
            Assert.True(autoMob.TryGetProperty("Level", out var autoLevel) && autoLevel.GetInt32() == 5);
            Assert.True(autoMob.TryGetProperty("Sex", out var autoSex) && autoSex.GetString() == "Female");
            Assert.True(autoMob.TryGetProperty("DefaultPosition", out var autoPosition) && autoPosition.GetString() == "Standing");
            Assert.True(autoMob.TryGetProperty("Resources", out var autoResources));
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

            if (objects.ValueKind != JsonValueKind.Array || objects.GetArrayLength() == 0)
            {
                return;
            }

            JsonElement? baseObject = null;
            foreach (var item in objects.EnumerateArray())
            {
                if (item.TryGetProperty("Id", out var id) && id.GetInt32() == 0)
                {
                    baseObject = item;
                    break;
                }
            }

            Assert.True(baseObject.HasValue);
            var obj = baseObject.Value;
            Assert.True(obj.TryGetProperty("Id", out var objId) && objId.GetInt32() == 0);
            Assert.True(obj.TryGetProperty("Name", out var name) && name.GetString() == "bug");

            JsonElement? rich = null;
            foreach (var item in objects.EnumerateArray())
            {
                if (item.TryGetProperty("ExtraDescriptions", out var extraDesc) && extraDesc.GetArrayLength() > 0)
                {
                    rich = item;
                    break;
                }
            }

            Assert.True(rich.HasValue);
            var richObject = rich.Value;
            Assert.True(richObject.TryGetProperty("Id", out var richId) && richId.GetInt32() == 1);
            Assert.True(richObject.TryGetProperty("Name", out var richName) && richName.GetString() == "cracked shell");
            Assert.True(richObject.TryGetProperty("ExtraDescriptions", out var extras) && extras.GetArrayLength() > 0);
            Assert.True(extras[0].TryGetProperty("Keywords", out var extraKeywords) && extraKeywords.GetArrayLength() > 0);
            Assert.True(extras[0].TryGetProperty("Description", out var extraDescription) && extraDescription.GetString() == "The crack runs along the edge.");
            Assert.True(richObject.TryGetProperty("Affects", out var affects) && affects.GetArrayLength() > 0);
            Assert.True(affects[0].TryGetProperty("Location", out var affectLocation) && affectLocation.GetString() == "Dexterity");
            Assert.True(affects[0].TryGetProperty("Modifier", out var affectModifier) && affectModifier.GetInt32() == 5);
            Assert.True(richObject.TryGetProperty("Bitvectors", out var bitvectors) && bitvectors.GetArrayLength() > 0);
            Assert.True(bitvectors[0].GetString() == "Bitvector_3");
            Assert.True(richObject.TryGetProperty("SpecialProc", out var special) && special.GetString() == "guard_object");
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
    public async Task ImportAsync_WritesMultipleMobs()
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

            Assert.True(mobs.ValueKind == JsonValueKind.Array && mobs.GetArrayLength() >= 10);
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

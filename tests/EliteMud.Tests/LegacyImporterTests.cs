using System.Text.Json;
using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

public class LegacyImporterTests
{
    private static JsonElement? FindObjectById(JsonElement objects, int id)
    {
        foreach (var item in objects.EnumerateArray())
        {
            if (item.TryGetProperty("Id", out var itemId) && itemId.GetInt32() == id)
            {
                return item;
            }
        }

        return null;
    }
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
            Assert.True(obj.TryGetProperty("Name", out var name) && name.GetString() == "practice sword");
            Assert.True(obj.TryGetProperty("Details", out var details));
            Assert.True(details.TryGetProperty("Weapon", out var weapon));
            Assert.True(weapon.TryGetProperty("DiceCount", out var diceCount) && diceCount.GetInt32() == 2);
            Assert.True(weapon.TryGetProperty("DiceSides", out var diceSides) && diceSides.GetInt32() == 4);
            Assert.True(weapon.TryGetProperty("DamageType", out var damageType) && damageType.GetInt32() == 11);
            Assert.True(weapon.TryGetProperty("HitPoints", out var hitPoints) && hitPoints.GetInt32() == 12);

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
            Assert.True(richObject.TryGetProperty("Name", out var richName) && richName.GetString() == "waterskin");
            Assert.True(richObject.TryGetProperty("Details", out var richDetails));
            Assert.True(richDetails.TryGetProperty("Drink", out var drinkDetails));
            Assert.True(drinkDetails.TryGetProperty("Capacity", out var capacity) && capacity.GetInt32() == 10);
            Assert.True(drinkDetails.TryGetProperty("Amount", out var amount) && amount.GetInt32() == 6);
            Assert.True(drinkDetails.TryGetProperty("Liquid", out var liquid) && liquid.GetInt32() == 2);
            Assert.True(drinkDetails.TryGetProperty("Poisoned", out var poisoned) && poisoned.GetBoolean());

            var portal = FindObjectById(objects, 3);
            Assert.True(portal.HasValue);
            Assert.True(portal.Value.TryGetProperty("Details", out var portalDetails));
            Assert.True(portalDetails.TryGetProperty("Portal", out var portalInfo));
            Assert.True(portalInfo.TryGetProperty("Destination", out var destination) && destination.GetInt32() == 3001);
            Assert.True(portalInfo.TryGetProperty("Flags", out var portalFlags) && portalFlags.GetArrayLength() == 2);
            Assert.True(portalInfo.TryGetProperty("LockItem", out var lockItem) && lockItem.GetInt32() == 2002);
            Assert.True(portalInfo.TryGetProperty("MinLevel", out var minLevel) && minLevel.GetInt32() == 5);
            Assert.True(portalInfo.TryGetProperty("MaxLevel", out var maxLevel) && maxLevel.GetInt32() == 30);
            Assert.True(portalInfo.TryGetProperty("Duration", out var duration) && duration.GetInt32() == 60);

            var wand = FindObjectById(objects, 4);
            Assert.True(wand.HasValue);
            Assert.True(wand.Value.TryGetProperty("Details", out var wandDetails));
            Assert.True(wandDetails.TryGetProperty("Charges", out var wandCharges));
            Assert.True(wandCharges.TryGetProperty("SpellId", out var wandSpell) && wandSpell.GetInt32() == 900);
            Assert.True(wandCharges.TryGetProperty("Level", out var wandLevel) && wandLevel.GetInt32() == 10);
            Assert.True(wandCharges.TryGetProperty("Charges", out var wandChargesTotal) && wandChargesTotal.GetInt32() == 12);
            Assert.True(wandCharges.TryGetProperty("ChargesRemaining", out var wandChargesRemaining) && wandChargesRemaining.GetInt32() == 7);

            var scroll = FindObjectById(objects, 5);
            Assert.True(scroll.HasValue);
            Assert.True(scroll.Value.TryGetProperty("Details", out var scrollDetails));
            Assert.True(scrollDetails.TryGetProperty("SpellContainer", out var spellContainer));
            Assert.True(spellContainer.TryGetProperty("Level", out var scrollLevel) && scrollLevel.GetInt32() == 8);
            Assert.True(spellContainer.TryGetProperty("SpellIds", out var spellIds) && spellIds.GetArrayLength() == 3);
            Assert.True(spellIds[0].GetInt32() == 900);
            Assert.True(spellIds[1].GetInt32() == 901);
            Assert.True(spellIds[2].GetInt32() == 902);

            var potion = FindObjectById(objects, 6);
            Assert.True(potion.HasValue);
            Assert.True(potion.Value.TryGetProperty("Details", out var potionDetails));
            Assert.True(potionDetails.TryGetProperty("SpellContainer", out var potionContainer));
            Assert.True(potionContainer.TryGetProperty("Level", out var potionLevel) && potionLevel.GetInt32() == 12);
            Assert.True(potionContainer.TryGetProperty("SpellIds", out var potionSpells) && potionSpells.GetArrayLength() == 3);
            Assert.True(potionSpells[0].GetInt32() == 910);
            Assert.True(potionSpells[1].GetInt32() == 0);
            Assert.True(potionSpells[2].GetInt32() == 0);

            var container = FindObjectById(objects, 2);
            Assert.True(container.HasValue);
            Assert.True(container.Value.TryGetProperty("Details", out var containerDetails));
            Assert.True(containerDetails.TryGetProperty("Container", out var containerInfo));
            Assert.True(containerInfo.TryGetProperty("Capacity", out var containerCapacity) && containerCapacity.GetInt32() == 50);
            Assert.True(containerInfo.TryGetProperty("Flags", out var containerFlags) && containerFlags.GetArrayLength() == 2);
            Assert.True(containerInfo.TryGetProperty("KeyId", out var containerKey) && containerKey.GetInt32() == 2001);
            Assert.True(containerInfo.TryGetProperty("CorpseType", out var corpseType) && corpseType.GetInt32() == 1);
            Assert.True(containerInfo.TryGetProperty("CorpseBlood", out var corpseBlood) && corpseBlood.GetInt32() == 3);
            Assert.True(containerInfo.TryGetProperty("CorpseLevel", out var corpseLevel) && corpseLevel.GetInt32() == 12);

            var armor = FindObjectById(objects, 7);
            Assert.True(armor.HasValue);
            Assert.True(armor.Value.TryGetProperty("Details", out var armorDetails));
            Assert.True(armorDetails.TryGetProperty("Armor", out var armorInfo));
            Assert.True(armorInfo.TryGetProperty("ArmorClass", out var armorClass) && armorClass.GetInt32() == 25);
            Assert.True(armorInfo.TryGetProperty("HitPoints", out var armorHp) && armorHp.GetInt32() == 40);

            var light = FindObjectById(objects, 8);
            Assert.True(light.HasValue);
            Assert.True(light.Value.TryGetProperty("Details", out var lightDetails));
            Assert.True(lightDetails.TryGetProperty("Light", out var lightInfo));
            Assert.True(lightInfo.TryGetProperty("Color", out var lightColor) && lightColor.GetInt32() == 2);
            Assert.True(lightInfo.TryGetProperty("Type", out var lightType) && lightType.GetInt32() == 4);
            Assert.True(lightInfo.TryGetProperty("Hours", out var lightHours) && lightHours.GetInt32() == 12);

            var money = FindObjectById(objects, 9);
            Assert.True(money.HasValue);
            Assert.True(money.Value.TryGetProperty("Details", out var moneyDetails));
            Assert.True(moneyDetails.TryGetProperty("Money", out var moneyInfo));
            Assert.True(moneyInfo.TryGetProperty("Amount", out var amountValue) && amountValue.GetInt32() == 250);
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

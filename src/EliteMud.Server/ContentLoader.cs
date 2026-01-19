using System.Text.Json;
using EliteMud.Game;

namespace EliteMud.Server;

internal static class ContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WorldDefinition? LoadWorld(string contentRoot)
    {
        var roomsPath = Path.Combine(contentRoot, "rooms", "rooms.json");
        if (!File.Exists(roomsPath))
        {
            return null;
        }

        RoomsFile? file;
        try
        {
            var json = File.ReadAllText(roomsPath);
            file = JsonSerializer.Deserialize<RoomsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load rooms: {exception.Message}");
            return null;
        }

        if (file?.Rooms is null || file.Rooms.Count == 0)
        {
            return null;
        }

        var rooms = new Dictionary<int, RoomDefinition>();
        foreach (var room in file.Rooms)
        {
            var exits = new List<ExitDefinition>();
            if (room.Exits is not null)
            {
                foreach (var exit in room.Exits)
                {
                    if (!Enum.TryParse<Direction>(exit.Direction ?? string.Empty, true, out var direction))
                    {
                        continue;
                    }

                    exits.Add(new ExitDefinition(direction, exit.TargetId));
                }
            }

            rooms[room.Id] = new RoomDefinition(room.Id, room.Name ?? "", room.Description ?? "", exits);
        }

        return new WorldDefinition(rooms);
    }

    public static IReadOnlyList<ScriptDefinition> LoadScripts(string contentRoot)
    {
        var scriptsPath = Path.Combine(contentRoot, "scripts", "scripts.json");
        if (!File.Exists(scriptsPath))
        {
            return Array.Empty<ScriptDefinition>();
        }

        ScriptsFile? file;
        try
        {
            var json = File.ReadAllText(scriptsPath);
            file = JsonSerializer.Deserialize<ScriptsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load scripts: {exception.Message}");
            return Array.Empty<ScriptDefinition>();
        }

        if (file?.Scripts is null || file.Scripts.Count == 0)
        {
            return Array.Empty<ScriptDefinition>();
        }

        var scripts = new List<ScriptDefinition>();
        foreach (var script in file.Scripts)
        {
            if (string.IsNullOrWhiteSpace(script.Id) || string.IsNullOrWhiteSpace(script.Hook))
            {
                continue;
            }

            scripts.Add(new ScriptDefinition(script.Id, script.Hook, script.Body ?? string.Empty, script.When?.RoomId));
        }

        return scripts;
    }

    public static IReadOnlyList<MobDefinition> LoadMobs(string contentRoot)
    {
        var mobsPath = Path.Combine(contentRoot, "mobs", "mobs.json");
        if (!File.Exists(mobsPath))
        {
            return Array.Empty<MobDefinition>();
        }

        MobsFile? file;
        try
        {
            var json = File.ReadAllText(mobsPath);
            file = JsonSerializer.Deserialize<MobsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load mobs: {exception.Message}");
            return Array.Empty<MobDefinition>();
        }

        if (file?.Mobs is null || file.Mobs.Count == 0)
        {
            return Array.Empty<MobDefinition>();
        }

        var mobs = new List<MobDefinition>();
        foreach (var mob in file.Mobs)
        {
            var stats = mob.Stats ?? new StatContent();
            mobs.Add(new MobDefinition(
                mob.Id,
                mob.Name ?? string.Empty,
                mob.ShortDescription ?? string.Empty,
                mob.LongDescription ?? string.Empty,
                mob.Description ?? string.Empty,
                mob.Level,
                mob.Race ?? string.Empty,
                mob.Class ?? string.Empty,
                mob.Flags ?? new List<string>(),
                new StatBlock(
                    stats.Strength,
                    stats.Dexterity,
                    stats.Intelligence,
                    stats.Wisdom,
                    stats.Constitution,
                    stats.Charisma),
                mob.Resistances ?? new List<string>(),
                mob.Skills ?? new List<string>()));
        }

        return mobs;
    }

    public static IReadOnlyList<ObjectDefinition> LoadObjects(string contentRoot)
    {
        var objectsPath = Path.Combine(contentRoot, "objects", "objects.json");
        if (!File.Exists(objectsPath))
        {
            return Array.Empty<ObjectDefinition>();
        }

        ObjectsFile? file;
        try
        {
            var json = File.ReadAllText(objectsPath);
            file = JsonSerializer.Deserialize<ObjectsFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load objects: {exception.Message}");
            return Array.Empty<ObjectDefinition>();
        }

        if (file?.Objects is null || file.Objects.Count == 0)
        {
            return Array.Empty<ObjectDefinition>();
        }

        var objects = new List<ObjectDefinition>();
        foreach (var obj in file.Objects)
        {
            objects.Add(new ObjectDefinition(
                obj.Id,
                obj.Name ?? string.Empty,
                obj.ShortDescription ?? string.Empty,
                obj.LongDescription ?? string.Empty,
                obj.Description ?? string.Empty,
                obj.Type ?? string.Empty,
                obj.WearSlots ?? new List<string>(),
                obj.Flags ?? new List<string>(),
                obj.Details,
                obj.Values ?? new List<int>(),
                obj.Weight,
                obj.Cost));
        }

        return objects;
    }

    public static IReadOnlyList<ZoneDefinition> LoadZones(string contentRoot)
    {
        var zonesPath = Path.Combine(contentRoot, "zones", "zones.json");
        if (!File.Exists(zonesPath))
        {
            return Array.Empty<ZoneDefinition>();
        }

        ZonesFile? file;
        try
        {
            var json = File.ReadAllText(zonesPath);
            file = JsonSerializer.Deserialize<ZonesFile>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to load zones: {exception.Message}");
            return Array.Empty<ZoneDefinition>();
        }

        if (file?.Zones is null || file.Zones.Count == 0)
        {
            return Array.Empty<ZoneDefinition>();
        }

        var zones = new List<ZoneDefinition>();
        foreach (var zone in file.Zones)
        {
            var resets = new List<ZoneResetDefinition>();
            if (zone.ResetCommands is not null)
            {
                foreach (var command in zone.ResetCommands)
                {
                    resets.Add(ConvertResetCommand(command));
                }
            }

            var roomRange = zone.RoomRange ?? new RoomRangeContent();
            zones.Add(new ZoneDefinition(
                zone.Id,
                zone.Name ?? string.Empty,
                new RoomRange(roomRange.Min, roomRange.Max),
                zone.ResetMode ?? string.Empty,
                resets));
        }

        return zones;
    }

    public static (WorldDefinition? World, IReadOnlyList<MobDefinition> Mobs, IReadOnlyList<ObjectDefinition> Objects, IReadOnlyList<ZoneDefinition> Zones) LoadFromZoneFiles(string zonesDirectory)
    {
        if (!Directory.Exists(zonesDirectory))
        {
            Console.WriteLine($"Zone directory not found: {zonesDirectory}");
            return (null, Array.Empty<MobDefinition>(), Array.Empty<ObjectDefinition>(), Array.Empty<ZoneDefinition>());
        }

        var zoneFiles = Directory.GetFiles(zonesDirectory, "zone_*.json");
        if (zoneFiles.Length == 0)
        {
            Console.WriteLine($"No zone files found in: {zonesDirectory}");
            return (null, Array.Empty<MobDefinition>(), Array.Empty<ObjectDefinition>(), Array.Empty<ZoneDefinition>());
        }

        Console.WriteLine($"Loading {zoneFiles.Length} zone files...");

        var allRooms = new Dictionary<int, RoomDefinition>();
        var allMobs = new Dictionary<int, MobDefinition>();
        var allObjects = new Dictionary<int, ObjectDefinition>();
        var allZones = new List<ZoneDefinition>();

        foreach (var zoneFile in zoneFiles)
        {
            try
            {
                var json = File.ReadAllText(zoneFile);
                var zoneData = JsonSerializer.Deserialize<ZoneGroupedFile>(json, JsonOptions);

                if (zoneData is null)
                {
                    Console.WriteLine($"  Skipped (null): {Path.GetFileName(zoneFile)}");
                    continue;
                }

                // Load rooms
                if (zoneData.Rooms is not null)
                {
                    foreach (var room in zoneData.Rooms)
                    {
                        var exits = new List<ExitDefinition>();
                        if (room.Exits is not null)
                        {
                            foreach (var exit in room.Exits)
                            {
                                if (Enum.TryParse<Direction>(exit.Direction ?? string.Empty, true, out var direction))
                                {
                                    exits.Add(new ExitDefinition(direction, exit.TargetId));
                                }
                            }
                        }

                        allRooms[room.Id] = new RoomDefinition(room.Id, room.Name ?? "", room.Description ?? "", exits);
                    }
                }

                // Load mobs
                if (zoneData.Mobs is not null)
                {
                    foreach (var mob in zoneData.Mobs)
                    {
                        var mobDef = ParseMobDefinition(mob);
                        if (mobDef is not null)
                        {
                            allMobs[mobDef.Id] = mobDef;
                        }
                    }
                }

                // Load objects
                if (zoneData.Objects is not null)
                {
                    foreach (var obj in zoneData.Objects)
                    {
                        var objectDef = ParseObjectDefinition(obj);
                        if (objectDef is not null)
                        {
                            allObjects[objectDef.Id] = objectDef;
                        }
                    }
                }

                // Load zone definition
                if (zoneData.Zone is not null)
                {
                    var resets = new List<ZoneResetDefinition>();
                    if (zoneData.Zone.ResetCommands is not null)
                    {
                        foreach (var command in zoneData.Zone.ResetCommands)
                        {
                            resets.Add(ConvertResetCommand(command));
                        }
                    }

                    var roomRange = zoneData.Zone.RoomRange ?? new RoomRangeContent();
                    var zoneDef = new ZoneDefinition(
                        zoneData.Zone.Id,
                        zoneData.Zone.Name ?? string.Empty,
                        new RoomRange(roomRange.Min, roomRange.Max),
                        zoneData.Zone.ResetMode ?? string.Empty,
                        resets);

                    allZones.Add(zoneDef);
                }

                Console.WriteLine($"  Loaded: {Path.GetFileName(zoneFile)} ({zoneData.Rooms?.Count ?? 0} rooms, {zoneData.Mobs?.Count ?? 0} mobs, {zoneData.Objects?.Count ?? 0} objects)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error loading {Path.GetFileName(zoneFile)}: {ex.Message}");
            }
        }

        Console.WriteLine($"Total: {allRooms.Count} rooms, {allMobs.Count} mobs, {allObjects.Count} objects, {allZones.Count} zones");

        var world = allRooms.Count > 0 ? new WorldDefinition(allRooms) : null;
        return (world, allMobs.Values.ToList(), allObjects.Values.ToList(), allZones);
    }

    private static MobDefinition? ParseMobDefinition(MobContent mob)
    {
        if (string.IsNullOrWhiteSpace(mob.Name))
        {
            return null;
        }

        var stats = new StatBlock(
            mob.Stats?.Strength ?? 10,
            mob.Stats?.Dexterity ?? 10,
            mob.Stats?.Intelligence ?? 10,
            mob.Stats?.Wisdom ?? 10,
            mob.Stats?.Constitution ?? 10,
            mob.Stats?.Charisma ?? 10);

        return new MobDefinition(
            mob.Id,
            mob.Name,
            mob.ShortDescription ?? string.Empty,
            mob.LongDescription ?? string.Empty,
            mob.Description ?? string.Empty,
            mob.Level,
            mob.Race ?? "Unknown",
            mob.Class ?? "Unknown",
            mob.Flags ?? new List<string>(),
            stats,
            mob.Resistances ?? new List<string>(),
            mob.Skills ?? new List<string>());
    }

    private static ObjectDefinition? ParseObjectDefinition(ObjectContent obj)
    {
        if (string.IsNullOrWhiteSpace(obj.Name))
        {
            return null;
        }

        ObjectDetails? details = null;
        if (obj.Details is not null)
        {
            // Parse object details from the JSON structure
            // For now, we'll skip the complex details parsing
            // TODO: Implement full ObjectDetails parsing
        }

        return new ObjectDefinition(
            obj.Id,
            obj.Name,
            obj.ShortDescription ?? string.Empty,
            obj.LongDescription ?? string.Empty,
            obj.Description ?? string.Empty,
            obj.Type ?? "Unknown",
            obj.WearFlags ?? new List<string>(),
            obj.ExtraFlags ?? new List<string>(),
            details,
            obj.Values ?? new List<int>(),
            obj.Weight,
            obj.Cost);
    }

    private static ZoneResetDefinition ConvertResetCommand(ZoneResetContent cmd)
    {
        // If modern format (semantic fields), use them directly
        if (cmd.Type is not null)
        {
            return new ZoneResetDefinition(
                cmd.Type,
                cmd.ObjectId,
                cmd.MobId,
                cmd.RoomId,
                cmd.MaxExisting,
                cmd.SpawnChance,
                cmd.EquipSlot,
                cmd.ContainerId,
                cmd.DoorDirection,
                cmd.DoorState,
                cmd.IfFlag == 1);
        }

        // Legacy format: map Command + Args to semantic fields
        var command = cmd.Command ?? string.Empty;
        var ifFlag = cmd.IfFlag == 1;

        return command switch
        {
            "M" => new ZoneResetDefinition(
                "LoadMob",
                ObjectId: null,
                MobId: cmd.Arg1,
                RoomId: cmd.Arg3,
                MaxExisting: cmd.Arg2,
                SpawnChance: null,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "O" => new ZoneResetDefinition(
                "LoadObject",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: cmd.Arg3,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "E" => new ZoneResetDefinition(
                "EquipMob",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: null,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: cmd.Arg3,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "G" => new ZoneResetDefinition(
                "GiveMob",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: null,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "P" => new ZoneResetDefinition(
                "PutObject",
                ObjectId: cmd.Arg1,
                MobId: null,
                RoomId: null,
                MaxExisting: null,
                SpawnChance: cmd.Arg2,
                EquipSlot: null,
                ContainerId: cmd.Arg3,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            "D" => new ZoneResetDefinition(
                "DoorState",
                ObjectId: null,
                MobId: null,
                RoomId: cmd.Arg1,
                MaxExisting: null,
                SpawnChance: null,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: cmd.Arg2,
                DoorState: cmd.Arg3,
                ifFlag),

            "R" => new ZoneResetDefinition(
                "RemoveObject",
                ObjectId: cmd.Arg2,
                MobId: null,
                RoomId: cmd.Arg1,
                MaxExisting: null,
                SpawnChance: null,
                EquipSlot: null,
                ContainerId: null,
                DoorDirection: null,
                DoorState: null,
                ifFlag),

            _ => new ZoneResetDefinition(
                command,
                null, null, null, null, null, null, null, null, null, ifFlag)
        };
    }

    private static string JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private sealed class RoomsFile
    {
        public List<RoomContent> Rooms { get; set; } = new();
    }

    private sealed class RoomContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<ExitContent>? Exits { get; set; }
    }

    private sealed class ExitContent
    {
        public string? Direction { get; set; }
        public int TargetId { get; set; }
    }

    private sealed class ScriptsFile
    {
        public List<ScriptContent> Scripts { get; set; } = new();
    }

    private sealed class ScriptContent
    {
        public string? Id { get; set; }
        public string? Hook { get; set; }
        public string? Body { get; set; }
        public ScriptWhen? When { get; set; }
    }

    private sealed class ScriptWhen
    {
        public int? RoomId { get; set; }
    }

    private sealed class MobsFile
    {
        public List<MobContent> Mobs { get; set; } = new();
    }

    private sealed class MobContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? Description { get; set; }
        public int Level { get; set; }
        public string? Race { get; set; }
        public string? Class { get; set; }
        public List<string>? Flags { get; set; }
        public StatContent? Stats { get; set; }
        public List<string>? Resistances { get; set; }
        public List<string>? Skills { get; set; }
    }

    private sealed class StatContent
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Constitution { get; set; }
        public int Charisma { get; set; }
    }

    private sealed class ObjectsFile
    {
        public List<ObjectContent> Objects { get; set; } = new();
    }

    private sealed class ObjectContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public List<string>? WearSlots { get; set; }
        public List<string>? WearFlags { get; set; } // Legacy format
        public List<string>? Flags { get; set; }
        public List<string>? ExtraFlags { get; set; } // Legacy format
        public List<int>? Values { get; set; }
        public ObjectDetails? Details { get; set; }
        public int Weight { get; set; }
        public int Cost { get; set; }
    }

    private sealed class ZonesFile
    {
        public List<ZoneContent> Zones { get; set; } = new();
    }

    private sealed class ZoneGroupedFile
    {
        public ZoneContent? Zone { get; set; }
        public List<RoomContent>? Rooms { get; set; }
        public List<MobContent>? Mobs { get; set; }
        public List<ObjectContent>? Objects { get; set; }
    }

    private sealed class ZoneContent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public RoomRangeContent? RoomRange { get; set; }
        public string? ResetMode { get; set; }
        public List<ZoneResetContent>? ResetCommands { get; set; }
    }

    private sealed class RoomRangeContent
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    private sealed class ZoneResetContent
    {
        public string? Type { get; set; }
        public string? Command { get; set; }
        public int? IfFlag { get; set; }
        public int? Arg1 { get; set; }
        public int? Arg2 { get; set; }
        public int? Arg3 { get; set; }
        
        // Semantic fields (for modern JSON format)
        public int? MobId { get; set; }
        public int? ObjectId { get; set; }
        public int? RoomId { get; set; }
        public int? MaxExisting { get; set; }
        public int? SpawnChance { get; set; }
        public int? EquipSlot { get; set; }
        public int? ContainerId { get; set; }
        public int? DoorDirection { get; set; }
        public int? DoorState { get; set; }
    }
}

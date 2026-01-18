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
                    resets.Add(new ZoneResetDefinition(
                        command.Type ?? string.Empty,
                        command.MobId,
                        command.RoomId,
                        command.MaxExisting));
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
        public List<string>? Flags { get; set; }
        public List<int>? Values { get; set; }
        public ObjectDetails? Details { get; set; }
        public int Weight { get; set; }
        public int Cost { get; set; }
    }

    private sealed class ZonesFile
    {
        public List<ZoneContent> Zones { get; set; } = new();
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
        public int? MobId { get; set; }
        public int? RoomId { get; set; }
        public int? MaxExisting { get; set; }
    }
}

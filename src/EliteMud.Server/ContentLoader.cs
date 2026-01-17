using System;
using System.Collections.Generic;
using System.IO;
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
}

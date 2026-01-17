using System;
using System.Collections.Generic;

namespace EliteMud.Game;

public enum Direction
{
    North,
    East,
    South,
    West,
    Up,
    Down
}

public sealed record ExitDefinition(Direction Direction, int TargetRoomId);

public sealed record RoomDefinition(int Id, string Name, string Description, IReadOnlyList<ExitDefinition> Exits);

public sealed record ScriptDefinition(string Id, string Hook, string Body, int? RoomId);

public sealed record StatBlock(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Wisdom,
    int Constitution,
    int Charisma);

public sealed record MobDefinition(
    int Id,
    string Name,
    string ShortDescription,
    string LongDescription,
    string Description,
    int Level,
    string Race,
    string Class,
    IReadOnlyList<string> Flags,
    StatBlock Stats,
    IReadOnlyList<string> Resistances,
    IReadOnlyList<string> Skills);

public sealed record ObjectDefinition(
    int Id,
    string Name,
    string ShortDescription,
    string LongDescription,
    string Description,
    string Type,
    IReadOnlyList<string> WearSlots,
    IReadOnlyList<string> Flags,
    IReadOnlyDictionary<string, string> Values,
    int Weight,
    int Cost);

public sealed record RoomRange(int Min, int Max);

public sealed record ZoneResetDefinition(
    string Type,
    int? MobId,
    int? RoomId,
    int? MaxExisting);

public sealed record ZoneDefinition(
    int Id,
    string Name,
    RoomRange RoomRange,
    string ResetMode,
    IReadOnlyList<ZoneResetDefinition> ResetCommands);

public sealed class PlayerState
{
    public PlayerState(int id, string name, int roomId)
    {
        Id = id;
        Name = name;
        RoomId = roomId;
    }

    public int Id { get; }

    public string Name { get; }

    public int RoomId { get; set; }
}

public sealed class WorldDefinition
{
    private readonly IReadOnlyDictionary<int, RoomDefinition> _rooms;

    public WorldDefinition(IReadOnlyDictionary<int, RoomDefinition> rooms)
    {
        _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public IReadOnlyDictionary<int, RoomDefinition> Rooms => _rooms;

    public RoomDefinition GetRoom(int id)
    {
        if (!_rooms.TryGetValue(id, out var room))
        {
            throw new KeyNotFoundException($"Room {id} not found.");
        }

        return room;
    }

    public bool TryMove(int currentRoomId, Direction direction, out int targetRoomId)
    {
        var room = GetRoom(currentRoomId);
        foreach (var exit in room.Exits)
        {
            if (exit.Direction == direction)
            {
                targetRoomId = exit.TargetRoomId;
                return true;
            }
        }

        targetRoomId = currentRoomId;
        return false;
    }
}

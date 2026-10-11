using EliteMud.Game;

namespace EliteMud.Application.World.Validation;

/// <summary>
/// Checks loaded world content for broken references and unreachable content.
/// Runs on the same definitions the server uses, so it sees exactly what the game would.
/// </summary>
public static class ContentValidator
{
    // Legacy: mortal_start_room (Temple of Midgaard), the start point for reachability
    public const int StartRoomId = 3001;

    private const int NoRoom = -1;

    public static IReadOnlyList<ContentFinding> Validate(
        WorldDefinition world,
        IReadOnlyDictionary<int, MobDefinition> mobs,
        IReadOnlyDictionary<int, ObjectDefinition> objects,
        IReadOnlyList<ZoneDefinition> zones,
        IReadOnlyList<AcceptedFinding>? accepted = null)
    {
        var findings = new List<ContentFinding>();
        var zoneOfRoom = BuildRoomZoneLookup(world, zones);

        CheckExits(world, objects, zoneOfRoom, findings);
        foreach (var zone in zones)
        {
            CheckResets(zone, world, mobs, objects, findings);
        }

        CheckKeysAreCreated(world, zones, zoneOfRoom, findings);
        CheckReachability(world, zones, zoneOfRoom, findings);
        return ApplyAccepted(findings, accepted ?? Array.Empty<AcceptedFinding>());
    }

    /// <summary>
    /// Mark findings that are known and accepted, with the reason. An accept entry that matches nothing
    /// is reported, so the list can't silently go stale.
    /// </summary>
    private static IReadOnlyList<ContentFinding> ApplyAccepted(List<ContentFinding> findings, IReadOnlyList<AcceptedFinding> accepted)
    {
        var used = new HashSet<AcceptedFinding>();
        var result = new List<ContentFinding>(findings.Count);
        foreach (var finding in findings)
        {
            var match = accepted.FirstOrDefault(entry => entry.Matches(finding));
            if (match is null)
            {
                result.Add(finding);
                continue;
            }

            used.Add(match);
            result.Add(finding with { AcceptedReason = match.Reason });
        }

        foreach (var entry in accepted.Where(entry => !used.Contains(entry)))
        {
            result.Add(new(ContentSeverity.Warning, "Accepted finding not found", entry.Category,
                $"no finding matches location \"{entry.Location}\" / message \"{entry.Message}\"; remove or update the entry"));
        }

        return result;
    }

    private static void CheckExits(
        WorldDefinition world,
        IReadOnlyDictionary<int, ObjectDefinition> objects,
        Func<int, string> zoneOfRoom,
        List<ContentFinding> findings)
    {
        foreach (var room in world.Rooms.Values)
        {
            foreach (var exit in room.Exits)
            {
                var location = $"{zoneOfRoom(room.Id)}, room {room.Id} ({room.Name}), exit {exit.Direction.ToString().ToLowerInvariant()}";
                if (exit.TargetRoomId == NoRoom)
                {
                    findings.Add(new(ContentSeverity.Warning, "Exit leads nowhere", location, "target is -1 (NOWHERE)"));
                }
                else if (!world.Rooms.ContainsKey(exit.TargetRoomId))
                {
                    findings.Add(new(ContentSeverity.Error, "Exit to missing room", location, $"room {exit.TargetRoomId} doesn't exist"));
                }

                // Key 0 is how many legacy files say "no key"
                if (exit.KeyId is > 0 and var keyId && !objects.ContainsKey(keyId))
                {
                    findings.Add(new(ContentSeverity.Error, "Door key missing", location, $"key object {keyId} doesn't exist"));
                }
            }
        }
    }

    private static void CheckResets(
        ZoneDefinition zone,
        WorldDefinition world,
        IReadOnlyDictionary<int, MobDefinition> mobs,
        IReadOnlyDictionary<int, ObjectDefinition> objects,
        List<ContentFinding> findings)
    {
        for (var index = 0; index < zone.ResetCommands.Count; index++)
        {
            var reset = zone.ResetCommands[index];
            var location = $"zone {zone.Id} {zone.Name}, reset {index + 1} ({reset.Type})";

            void RequireRoom(int? roomId, string role)
            {
                if (roomId is >= 0 && !world.Rooms.ContainsKey(roomId.Value))
                {
                    findings.Add(new(ContentSeverity.Error, "Reset uses missing room", location, $"{role} {roomId} doesn't exist"));
                }
            }

            void RequireObject(int? objectId, string role)
            {
                if (objectId.HasValue && !objects.ContainsKey(objectId.Value))
                {
                    findings.Add(new(ContentSeverity.Error, "Reset uses missing object", location, $"{role} {objectId} doesn't exist"));
                }
            }

            switch (reset.Type)
            {
                case "LoadMob":
                    if (reset.MobId.HasValue && !mobs.ContainsKey(reset.MobId.Value))
                    {
                        findings.Add(new(ContentSeverity.Error, "Reset uses missing mob", location, $"mob {reset.MobId} doesn't exist"));
                    }

                    RequireRoom(reset.RoomId, "room");
                    break;

                case "LoadObject":
                    RequireObject(reset.ObjectId, "object");
                    RequireRoom(reset.RoomId, "room"); // -1 means "loaded nowhere", like legacy
                    break;

                case "EquipMob":
                case "GiveMob":
                    RequireObject(reset.ObjectId, "object");
                    break;

                case "PutObject":
                    RequireObject(reset.ObjectId, "object");
                    RequireObject(reset.ContainerId, "container");
                    break;

                case "DoorState":
                    CheckDoorReset(reset, world, location, findings);
                    break;

                case "ShuffleExits":
                    RequireRoom(reset.RoomId, "room");
                    break;

                case "LoadPortal":
                    RequireRoom(reset.RoomId, "room");
                    RequireObject(reset.ObjectId, "portal");
                    RequireRoom(reset.PortalDestination, "destination");
                    if (reset.ObjectId is { } portalId && objects.TryGetValue(portalId, out var portal)
                        && !string.Equals(portal.Type, "Portal", StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new(ContentSeverity.Error, "Portal reset on non-portal", location, $"object {portalId} is a {portal.Type}"));
                    }

                    break;

                case "ActivatePortal":
                    RequireRoom(reset.RoomId, "room");
                    if (reset.PortalKeyId is > 0 and var portalKey)
                    {
                        RequireObject(portalKey, "portal key");
                    }

                    break;

                case "RemoveObject":
                    RequireRoom(reset.RoomId, "room");
                    RequireObject(reset.ObjectId, "object");
                    break;

                default:
                    findings.Add(new(ContentSeverity.Warning, "Unknown reset type", location, $"the server ignores '{reset.Type}'"));
                    break;
            }
        }
    }

    /// <summary>
    /// Legacy 'D' (reset_zone() in db.c) sets the closed/locked bits on any exit, door or not; it only fails
    /// when there's no exit. Movement is blocked by "closed" alone, but open/close need a door, so closing
    /// an exit that isn't a door makes a barrier players can't open with commands.
    /// </summary>
    private static void CheckDoorReset(ZoneResetDefinition reset, WorldDefinition world, string location, List<ContentFinding> findings)
    {
        if (!reset.RoomId.HasValue || !world.Rooms.TryGetValue(reset.RoomId.Value, out var room))
        {
            findings.Add(new(ContentSeverity.Error, "Reset uses missing room", location, $"room {reset.RoomId} doesn't exist"));
            return;
        }

        if (reset.DoorDirection is not { } direction || direction < 0 || direction > (int)Direction.Down)
        {
            findings.Add(new(ContentSeverity.Error, "Door reset on missing exit", location, $"invalid direction {reset.DoorDirection}"));
            return;
        }

        var directionName = ((Direction)direction).ToString().ToLowerInvariant();
        var exit = room.Exits.FirstOrDefault(e => e.Direction == (Direction)direction);
        if (exit is null)
        {
            findings.Add(new(ContentSeverity.Error, "Door reset on missing exit", location,
                $"room {room.Id} has no exit to the {directionName}"));
        }
        else if (!exit.IsDoor && reset.DoorState is > 0)
        {
            findings.Add(new(ContentSeverity.Info, "Barrier exit", location,
                $"closes room {room.Id}'s {directionName} exit, which isn't a door; players can't open it with commands"));
        }
    }

    /// <summary>
    /// A door whose key exists but that nothing in the world ever creates can't be opened by players.
    /// Shops and special procedures aren't checked, so this is a warning.
    /// </summary>
    private static void CheckKeysAreCreated(
        WorldDefinition world,
        IReadOnlyList<ZoneDefinition> zones,
        Func<int, string> zoneOfRoom,
        List<ContentFinding> findings)
    {
        var created = new HashSet<int>();
        foreach (var reset in zones.SelectMany(zone => zone.ResetCommands))
        {
            if (reset.Type is "LoadObject" or "EquipMob" or "GiveMob" or "PutObject" && reset.ObjectId.HasValue)
            {
                created.Add(reset.ObjectId.Value);
            }
        }

        var doorsByKey = world.Rooms.Values
            .SelectMany(room => room.Exits.Where(exit => exit.KeyId is > 0).Select(exit => (Room: room, Key: exit.KeyId!.Value)))
            .Where(door => !created.Contains(door.Key))
            .GroupBy(door => door.Key);

        foreach (var group in doorsByKey)
        {
            var rooms = string.Join(", ", group.Select(door => door.Room.Id).Distinct().Order());
            findings.Add(new(ContentSeverity.Warning, "Door key never created", zoneOfRoom(group.First().Room.Id),
                $"key object {group.Key} opens doors in room(s) {rooms}, but no zone reset creates it (shops and special procedures not checked)"));
        }
    }

    /// <summary>
    /// Walk from the Temple through exits and placed portals. Zones with no reachable room are reported.
    /// Doors, keys and level limits are ignored, so this only finds content with no way in at all.
    /// </summary>
    private static void CheckReachability(
        WorldDefinition world,
        IReadOnlyList<ZoneDefinition> zones,
        Func<int, string> zoneOfRoom,
        List<ContentFinding> findings)
    {
        if (!world.Rooms.ContainsKey(StartRoomId))
        {
            findings.Add(new(ContentSeverity.Error, "Start room missing", $"room {StartRoomId}", "the Temple of Midgaard doesn't exist"));
            return;
        }

        var portalLinks = zones
            .SelectMany(zone => zone.ResetCommands)
            .Where(reset => reset.Type == "LoadPortal" && reset.RoomId.HasValue && reset.PortalDestination.HasValue)
            .ToLookup(reset => reset.RoomId!.Value, reset => reset.PortalDestination!.Value);

        var reached = new HashSet<int> { StartRoomId };
        var queue = new Queue<int>(reached);
        while (queue.Count > 0)
        {
            var roomId = queue.Dequeue();
            var targets = world.Rooms[roomId].Exits.Select(exit => exit.TargetRoomId).Concat(portalLinks[roomId]);
            foreach (var target in targets)
            {
                if (world.Rooms.ContainsKey(target) && reached.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        foreach (var zone in zones)
        {
            var zoneRooms = world.Rooms.Keys.Where(id => id >= zone.RoomRange.Min && id <= zone.RoomRange.Max).ToList();
            var reachable = zoneRooms.Count(reached.Contains);
            if (zoneRooms.Count > 0 && reachable == 0)
            {
                findings.Add(new(ContentSeverity.Warning, "Zone unreachable", $"zone {zone.Id} {zone.Name}",
                    $"none of its {zoneRooms.Count} rooms can be reached from room {StartRoomId} by exits or portals"));
            }
            else if (reachable < zoneRooms.Count)
            {
                findings.Add(new(ContentSeverity.Info, "Rooms unreachable", $"zone {zone.Id} {zone.Name}",
                    $"{zoneRooms.Count - reachable} of {zoneRooms.Count} rooms can't be reached by exits or portals"));
            }
        }
    }

    private static Func<int, string> BuildRoomZoneLookup(WorldDefinition world, IReadOnlyList<ZoneDefinition> zones)
    {
        var lookup = new Dictionary<int, string>();
        foreach (var roomId in world.Rooms.Keys)
        {
            var zone = zones.FirstOrDefault(z => roomId >= z.RoomRange.Min && roomId <= z.RoomRange.Max);
            lookup[roomId] = zone is null ? "no zone" : $"zone {zone.Id} {zone.Name}";
        }

        return roomId => lookup.TryGetValue(roomId, out var name) ? name : "no zone";
    }
}

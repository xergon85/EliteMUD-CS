using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Look;

public sealed class LookHandler
{
    private readonly IWorldState _worldState;
    private readonly Func<IEnumerable<PlayerState>>? _getPlayersInRoom;

    public LookHandler(IWorldState worldState, Func<IEnumerable<PlayerState>>? getPlayersInRoom = null)
    {
        _worldState = worldState;
        _getPlayersInRoom = getPlayersInRoom;
    }

    public RoomView Handle(PlayerState player)
    {
        var room = _worldState.World.GetRoom(player.RoomId);
        var mobs = _worldState.GetMobsInRoom(room.Id);
        var mobLines = new List<string>();
        foreach (var mob in mobs)
        {
            var line = string.IsNullOrWhiteSpace(mob.Definition.LongDescription)
                ? mob.Definition.ShortDescription
                : mob.Definition.LongDescription;

            // Trim all whitespace and newlines
            line = line.Trim();

            if (!string.IsNullOrWhiteSpace(line))
            {
                // Add yellow color for NPCs (legacy: #Y...#N)
                mobLines.Add($"#Y{line}#N");
            }
        }

        var objects = _worldState.GetObjectsInRoom(room.Id);
        var objectLines = new List<string>();
        foreach (var obj in objects)
        {
            var line = string.IsNullOrWhiteSpace(obj.Definition.LongDescription)
                ? obj.Definition.ShortDescription
                : obj.Definition.LongDescription;

            // Trim all whitespace and newlines
            line = line?.Trim();

            if (!string.IsNullOrWhiteSpace(line))
            {
                // Add green color for objects (legacy: #G...#N)
                objectLines.Add($"#G{line}#N");
            }
        }

        // Get other players in the room (exclude self)
        var playerLines = new List<string>();
        if (_getPlayersInRoom != null)
        {
            var otherPlayers = _getPlayersInRoom()
                .Where(p => p.RoomId == player.RoomId && p.Name != player.Name);

            foreach (var otherPlayer in otherPlayers)
            {
                // Format: "<Name> is standing here." with cyan color
                playerLines.Add($"#C{otherPlayer.Name} is standing here.#N");
            }
        }

        return new RoomView(room.Name, room.Description, mobLines, objectLines, playerLines, BuildExitLine(room));
    }

    public CommandResult HandleLookAt(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Look at what?");
        }

        var room = _worldState.World.GetRoom(player.RoomId);

        // Try to find another player in room first
        // Note: Players don't use indexed targeting in legacy (can't do "look 2.player")
        if (_getPlayersInRoom != null)
        {
            var otherPlayers = _getPlayersInRoom()
                .Where(p => p.RoomId == player.RoomId && p.Name != player.Name);

            foreach (var otherPlayer in otherPlayers)
            {
                if (MatchesTarget(otherPlayer, target))
                {
                    return FormatPlayerDescription(otherPlayer, _worldState);
                }
            }
        }

        // Try to find object in room using indexed targeting (e.g., "look 2.corpse")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        var objects = _worldState.GetObjectsInRoom(room.Id);
        var obj = TargetParser.FindObject(objects, target);
        if (obj != null)
        {
            return FormatObjectDescription(obj.Definition);
        }

        // Try to find mob in room using indexed targeting (e.g., "look 2.guard")
        // Legacy: handler.c:1481-1501 (get_char_room_vis with get_number)
        var mobs = _worldState.GetMobsInRoom(room.Id);
        var mob = TargetParser.FindMob(mobs, target);
        if (mob != null)
        {
            return FormatMobDescription(mob);
        }

        return CommandResult.Fail("You don't see that here.");
    }

    private static bool MatchesTarget(PlayerState player, string target)
    {
        var targetLower = target.ToLowerInvariant();

        // Check if target matches player name (case-insensitive)
        return player.Name.ToLowerInvariant().StartsWith(targetLower);
    }

    private static CommandResult FormatObjectDescription(ObjectDefinition obj)
    {
        var description = obj.Description;
        return CommandResult.Ok(description.Trim());
    }

    private static CommandResult FormatMobDescription(MobInstance mob)
    {
        var description = mob.Definition.Description;
        var result = description.Trim();

        // Show equipped items
        if (mob.Equipment.Count > 0)
        {
            result += "\n\n" + mob.Definition.ShortDescription + " is using:";
            foreach (var (slot, item) in mob.Equipment)
            {
                result += $"\n  <{FormatSlot(slot)}> {item.Definition.ShortDescription}";
            }
        }

        return CommandResult.Ok(result);
    }

    private static CommandResult FormatPlayerDescription(PlayerState player, IWorldState worldState)
    {
        var result = new System.Text.StringBuilder();

        // Show player's custom description if set, otherwise default message
        if (!string.IsNullOrWhiteSpace(player.Description))
        {
            result.AppendLine(player.Description.Trim());
        }
        else
        {
            result.AppendLine("You see nothing special about them.");
        }

        // Show health condition (legacy: diag_char_to_char)
        var healthCondition = GetHealthCondition(player);
        result.AppendLine();
        result.Append(player.Name);
        result.Append(" the ");
        result.Append(player.Race);
        result.Append(healthCondition);

        // Show equipped items if any
        if (player.EquipmentSlotToObjectId.Count > 0)
        {
            result.AppendLine();
            result.AppendLine();
            result.Append(player.Name);
            result.Append(" is using:");

            foreach (var (slotId, objectInstanceId) in player.EquipmentSlotToObjectId.OrderBy(x => x.Key))
            {
                var obj = worldState.GetObjectInstance(objectInstanceId);
                if (obj != null)
                {
                    var slot = (EquipmentSlot)slotId;
                    result.AppendLine();
                    result.Append("  <");
                    result.Append(FormatSlot(slot));
                    result.Append("> ");
                    result.Append(obj.Definition.ShortDescription);
                }
            }
        }

        return CommandResult.Ok(result.ToString());
    }

    private static string GetHealthCondition(PlayerState player)
    {
        // Legacy formula: cond = (8 * current_hp) / max_hp
        // Returns conditions: excellent, few scratches, small wounds, quite wounded, nasty wounds, pretty hurt, awful, near death
        if (player.MaxHitPoints <= 0)
            return " is in unknown condition.";

        var cond = (8 * player.HitPoints) / player.MaxHitPoints;

        if (cond > 8) cond = 8;
        if (cond < 0) cond = 0;

        return cond switch
        {
            8 => " is in excellent condition.",
            7 => " has a few scratches.",
            6 => " has some small wounds and bruises.",
            5 => " has quite a few wounds.",
            4 => " has some big nasty wounds and scratches.",
            3 => " looks pretty hurt.",
            2 => " is in awful condition.",
            1 => " is near death.",
            0 => " is bleeding to death.",
            _ => " is in unknown condition."
        };
    }

    private static string FormatSlot(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Light => "used as light",
            EquipmentSlot.FingerRight => "worn on right finger",
            EquipmentSlot.FingerLeft => "worn on left finger",
            EquipmentSlot.Neck1 => "worn around neck",
            EquipmentSlot.Neck2 => "worn around neck",
            EquipmentSlot.Body => "worn on body",
            EquipmentSlot.Head => "worn on head",
            EquipmentSlot.Legs => "worn on legs",
            EquipmentSlot.Feet => "worn on feet",
            EquipmentSlot.Hands => "worn on hands",
            EquipmentSlot.Arms => "worn on arms",
            EquipmentSlot.Shield => "worn as shield",
            EquipmentSlot.About => "worn about body",
            EquipmentSlot.Waist => "worn about waist",
            EquipmentSlot.WristRight => "worn on right wrist",
            EquipmentSlot.WristLeft => "worn on left wrist",
            EquipmentSlot.Wield => "wielded",
            EquipmentSlot.Hold => "held",
            EquipmentSlot.Wield2 => "dual wielded",
            EquipmentSlot.BothHands => "held in both hands",
            _ => "worn"
        };
    }

    private static string BuildExitLine(RoomDefinition room)
    {
        if (room.Exits.Count == 0)
        {
            return "[Exits: #CNone!#N]";
        }

        var names = new List<string>(room.Exits.Count);
        foreach (var exit in room.Exits)
        {
            // Use single-letter abbreviations matching legacy (n/e/s/w/u/d)
            var dir = exit.Direction.ToString().ToLowerInvariant();
            names.Add(dir.Substring(0, 1));
        }

        // Legacy format: [Exits: #C{exits}#N]
        return $"[Exits: #C{string.Join("", names)}#N]";
    }
}

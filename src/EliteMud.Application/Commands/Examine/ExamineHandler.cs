using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Examine;

public sealed class ExamineHandler
{
    private readonly IWorldState _worldState;

    public ExamineHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Examine what?");
        }

        var room = _worldState.World.GetRoom(player.RoomId);

        // Try to find object in room
        var objects = _worldState.GetObjectsInRoom(room.Id);
        foreach (var obj in objects)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                return FormatObjectDescription(obj.Definition);
            }
        }

        // Try to find mob in room
        var mobs = _worldState.GetMobsInRoom(room.Id);
        foreach (var mob in mobs)
        {
            if (MatchesTarget(mob.Definition, target))
            {
                return FormatMobDescription(mob);
            }
        }

        return CommandResult.Fail("You don't see that here.");
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        
        // Check if target matches any keyword in the object name
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }

    private static bool MatchesTarget(MobDefinition mob, string target)
    {
        var targetLower = target.ToLowerInvariant();
        
        // Check if target matches any keyword in the mob name
        var keywords = mob.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }

    private static CommandResult FormatObjectDescription(ObjectDefinition obj)
    {
        var description = obj.Description ?? obj.LongDescription ?? obj.ShortDescription ?? "You see nothing special.";
        return CommandResult.Ok(description.Trim());
    }

    private static CommandResult FormatMobDescription(MobInstance mob)
    {
        var description = mob.Definition.Description ?? mob.Definition.LongDescription ?? mob.Definition.ShortDescription ?? "You see nothing special.";
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
}

using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Wear;

public sealed record WearResult(bool Success, string Message, ObjectDefinition? Object = null, List<ObjectDefinition>? Objects = null);

public sealed class WearHandler
{
    private readonly IWorldState _worldState;

    public WearHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public WearResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new WearResult(false, "Wear what?");
        }

        // Handle "wear all" - wear all wearable items in inventory
        if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return WearAll(player);
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory using indexed targeting (e.g., "2.helmet")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        var obj = TargetParser.FindObject(inventory, target);
        
        if (obj == null)
        {
            return new WearResult(false, "You don't have that.");
        }

        // Determine which slot(s) to try wearing it in
        var slots = DetermineSlots(obj.Definition);
        if (slots.Count == 0)
        {
            return new WearResult(false, $"{obj.Definition.ShortDescription} cannot be worn.");
        }

        // Try each potential slot until we find one that works
        var equipment = _worldState.GetPlayerEquipment(player);
        foreach (var slot in slots)
        {
            // Check if slot is available
            if (!equipment.ContainsKey(slot))
            {
                // Try to equip the object
                if (_worldState.EquipObject(player, obj.InstanceId, slot))
                {
                    // Return object so CommandHandler can use ActMessage
                    return new WearResult(true, string.Empty, obj.Definition);
                }
            }
        }

        // All slots occupied
        return new WearResult(false, $"You are already wearing something on your {GetSlotName(slots[0])}.");
    }

    private WearResult WearAll(PlayerState player)
    {
        var inventory = _worldState.GetPlayerInventory(player);
        var wornObjects = new List<ObjectDefinition>();
        var equipment = _worldState.GetPlayerEquipment(player);

        // Try to wear each wearable item in inventory
        foreach (var obj in inventory.ToList())
        {
            // Determine which slot(s) to try wearing it in
            var slots = DetermineSlots(obj.Definition);
            if (slots.Count == 0)
            {
                // Not wearable, skip silently (matches legacy behavior)
                continue;
            }

            // Try each potential slot until we find one that works
            bool equipped = false;
            foreach (var slot in slots)
            {
                // Check if slot is available
                if (!equipment.ContainsKey(slot))
                {
                    // Try to equip the object
                    if (_worldState.EquipObject(player, obj.InstanceId, slot))
                    {
                        wornObjects.Add(obj.Definition);
                        equipment = _worldState.GetPlayerEquipment(player); // Refresh equipment state
                        equipped = true;
                        break;
                    }
                }
            }
            
            if (!equipped)
            {
                // All slots occupied, skip silently (matches legacy behavior)
                continue;
            }
        }

        if (wornObjects.Count == 0)
        {
            return new WearResult(false, "You don't have anything you can wear.");
        }

        // Return list of worn objects so CommandHandler can use ActMessage for each
        return new WearResult(true, string.Empty, Objects: wornObjects);
    }

    /// <summary>
    /// Determine all potential equipment slots for an object.
    /// Returns slots in priority order (e.g., Neck1 before Neck2).
    /// Legacy: handler.c wear command tries primary slot, then alternative slots.
    /// </summary>
    private static List<EquipmentSlot> DetermineSlots(ObjectDefinition obj)
    {
        var slots = new List<EquipmentSlot>();
        
        // Map wear slot names to EquipmentSlot enum
        // Prioritize common single slots first
        if (obj.WearSlots.Contains("Body")) { slots.Add(EquipmentSlot.Body); return slots; }
        if (obj.WearSlots.Contains("Head")) { slots.Add(EquipmentSlot.Head); return slots; }
        if (obj.WearSlots.Contains("Legs")) { slots.Add(EquipmentSlot.Legs); return slots; }
        if (obj.WearSlots.Contains("Feet")) { slots.Add(EquipmentSlot.Feet); return slots; }
        if (obj.WearSlots.Contains("Hands")) { slots.Add(EquipmentSlot.Hands); return slots; }
        if (obj.WearSlots.Contains("Arms")) { slots.Add(EquipmentSlot.Arms); return slots; }
        if (obj.WearSlots.Contains("About")) { slots.Add(EquipmentSlot.About); return slots; }
        if (obj.WearSlots.Contains("Waist")) { slots.Add(EquipmentSlot.Waist); return slots; }
        if (obj.WearSlots.Contains("Shield")) { slots.Add(EquipmentSlot.Shield); return slots; }
        if (obj.WearSlots.Contains("Light")) { slots.Add(EquipmentSlot.Light); return slots; }
        
        // Handle dual slots (neck, wrist, finger) - try both slots
        if (obj.WearSlots.Contains("Neck") || obj.WearSlots.Contains("Neck1") || obj.WearSlots.Contains("Neck2"))
        {
            slots.Add(EquipmentSlot.Neck1);
            slots.Add(EquipmentSlot.Neck2);
            return slots;
        }
        
        if (obj.WearSlots.Contains("Wrist") || obj.WearSlots.Contains("WristRight") || obj.WearSlots.Contains("WristLeft"))
        {
            slots.Add(EquipmentSlot.WristRight);
            slots.Add(EquipmentSlot.WristLeft);
            return slots;
        }
        
        if (obj.WearSlots.Contains("Finger") || obj.WearSlots.Contains("FingerRight") || obj.WearSlots.Contains("FingerLeft"))
        {
            slots.Add(EquipmentSlot.FingerRight);
            slots.Add(EquipmentSlot.FingerLeft);
            return slots;
        }
        
        // Don't auto-wear weapons/held items - use wield/hold instead
        return slots;
    }

    private static string GetSlotName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Body => "body",
            EquipmentSlot.Head => "head",
            EquipmentSlot.Legs => "legs",
            EquipmentSlot.Feet => "feet",
            EquipmentSlot.Hands => "hands",
            EquipmentSlot.Arms => "arms",
            EquipmentSlot.About => "shoulders",
            EquipmentSlot.Waist => "waist",
            EquipmentSlot.Neck1 => "neck",
            EquipmentSlot.Neck2 => "neck",
            EquipmentSlot.WristRight => "right wrist",
            EquipmentSlot.WristLeft => "left wrist",
            EquipmentSlot.FingerRight => "right finger",
            EquipmentSlot.FingerLeft => "left finger",
            EquipmentSlot.Shield => "shield arm",
            EquipmentSlot.Light => "light source",
            _ => "body"
        };
    }
}

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

        // Determine which slot to wear it in
        var slot = DetermineSlot(obj.Definition);
        if (slot is null)
        {
            return new WearResult(false, $"{obj.Definition.ShortDescription} cannot be worn.");
        }

        // Try to equip the object
        if (_worldState.EquipObject(player, obj.InstanceId, slot.Value))
        {
            // Return object so CommandHandler can use ActMessage
            return new WearResult(true, string.Empty, obj.Definition);
        }
        else
        {
            return new WearResult(false, $"You are already wearing something on your {GetSlotName(slot.Value)}.");
        }
    }

    private WearResult WearAll(PlayerState player)
    {
        var inventory = _worldState.GetPlayerInventory(player);
        var wornObjects = new List<ObjectDefinition>();

        // Try to wear each wearable item in inventory
        foreach (var obj in inventory.ToList())
        {
            // Determine which slot to wear it in
            var slot = DetermineSlot(obj.Definition);
            if (slot is null)
            {
                // Not wearable, skip silently (matches legacy behavior)
                continue;
            }

            // Try to equip the object
            if (_worldState.EquipObject(player, obj.InstanceId, slot.Value))
            {
                wornObjects.Add(obj.Definition);
            }
            else
            {
                // Slot already occupied, skip silently (matches legacy behavior)
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

    private static EquipmentSlot? DetermineSlot(ObjectDefinition obj)
    {
        // Map wear slot names to EquipmentSlot enum
        // Prioritize common slots first
        if (obj.WearSlots.Contains("Body")) return EquipmentSlot.Body;
        if (obj.WearSlots.Contains("Head")) return EquipmentSlot.Head;
        if (obj.WearSlots.Contains("Legs")) return EquipmentSlot.Legs;
        if (obj.WearSlots.Contains("Feet")) return EquipmentSlot.Feet;
        if (obj.WearSlots.Contains("Hands")) return EquipmentSlot.Hands;
        if (obj.WearSlots.Contains("Arms")) return EquipmentSlot.Arms;
        if (obj.WearSlots.Contains("About")) return EquipmentSlot.About;
        if (obj.WearSlots.Contains("Waist")) return EquipmentSlot.Waist;
        
        // Handle both legacy single slots and split slots
        if (obj.WearSlots.Contains("Neck") || obj.WearSlots.Contains("Neck1")) return EquipmentSlot.Neck1;
        if (obj.WearSlots.Contains("Neck2")) return EquipmentSlot.Neck2;
        if (obj.WearSlots.Contains("Wrist") || obj.WearSlots.Contains("WristRight")) return EquipmentSlot.WristRight;
        if (obj.WearSlots.Contains("WristLeft")) return EquipmentSlot.WristLeft;
        if (obj.WearSlots.Contains("Finger") || obj.WearSlots.Contains("FingerRight")) return EquipmentSlot.FingerRight;
        if (obj.WearSlots.Contains("FingerLeft")) return EquipmentSlot.FingerLeft;
        if (obj.WearSlots.Contains("Shield")) return EquipmentSlot.Shield;
        if (obj.WearSlots.Contains("Light")) return EquipmentSlot.Light;
        
        // Don't auto-wear weapons/held items - use wield/hold instead
        return null;
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

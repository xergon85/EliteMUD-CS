using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Wear;

public sealed class WearHandler
{
    private readonly IWorldState _worldState;

    public WearHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Wear what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory using indexed targeting (e.g., "2.helmet")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        var obj = TargetParser.FindObject(inventory, target);
        
        if (obj == null)
        {
            return CommandResult.Fail("You don't have that.");
        }

        // Determine which slot to wear it in
        var slot = DetermineSlot(obj.Definition);
        if (slot is null)
        {
            return CommandResult.Fail($"{obj.Definition.ShortDescription} cannot be worn.");
        }

        // Try to equip the object
        if (_worldState.EquipObject(player, obj.InstanceId, slot.Value))
        {
            return CommandResult.Ok($"You wear {obj.Definition.ShortDescription}.");
        }
        else
        {
            return CommandResult.Fail($"You are already wearing something on your {GetSlotName(slot.Value)}.");
        }
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

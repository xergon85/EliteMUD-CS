using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Hold;

public sealed record HoldResult(bool Success, string Message, ObjectDefinition? Object = null);

public sealed class HoldHandler
{
    private readonly IWorldState _worldState;

    public HoldHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public HoldResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new HoldResult(false, "Hold what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory using indexed targeting (e.g., "2.torch")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        var obj = TargetParser.FindObject(inventory, target);
        
        if (obj == null)
        {
            return new HoldResult(false, "You don't have that.");
        }

        // Legacy EliteMUD behavior (see act.obj2.c:do_grab):
        // The 'hold' command accepts multiple item types:
        // - Light objects (Type==ITEM_LIGHT) → WEAR_LIGHT slot (requires only ITEM_TAKE)
        // - Weapons with Wield flag → WIELD slot (wield and hold are interchangeable for weapons)
        // - Items with Hold flag → HOLD slot (shields, orbs, etc.)
        EquipmentSlot slot;
        
        if (obj.Definition.Type == "Light")
        {
            // Light objects only need Take flag (legacy: wear_bitvectors[WEAR_LIGHT] = ITEM_TAKE)
            if (!obj.Definition.WearSlots.Contains("Take"))
            {
                return new HoldResult(false, $"{obj.Definition.ShortDescription} cannot be held.");
            }
            slot = EquipmentSlot.Light;
        }
        else if (obj.Definition.WearSlots.Contains("Wield") || 
                 obj.Definition.WearSlots.Contains("WieldTwoHanded") ||
                 obj.Definition.WearSlots.Contains("BothHands"))
        {
            // Weapons can be held (wield and hold are interchangeable in legacy)
            slot = obj.Definition.WearSlots.Contains("Wield") 
                ? EquipmentSlot.Wield 
                : EquipmentSlot.BothHands;
        }
        else if (obj.Definition.WearSlots.Contains("Hold"))
        {
            // Items with Hold flag (shields, orbs, etc.)
            slot = EquipmentSlot.Hold;
        }
        else
        {
            // Item cannot be held
            return new HoldResult(false, $"{obj.Definition.ShortDescription} cannot be held.");
        }

        // Try to equip the object
        if (_worldState.EquipObject(player, obj.InstanceId, slot))
        {
            return new HoldResult(true, string.Empty, obj.Definition);
        }
        else
        {
            var slotName = slot == EquipmentSlot.Light ? "light source" : "held item";
            return new HoldResult(false, $"You are already holding a {slotName}.");
        }
    }
}

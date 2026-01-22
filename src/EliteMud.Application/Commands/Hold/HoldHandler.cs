using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Hold;

public sealed record HoldResult(bool Success, string Message, ObjectDefinition? Object = null, ObjectDefinition? AlreadyEquipped = null);

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

        // Legacy EliteMUD behavior (see act.obj2.c:do_grab and perform_wear):
        // The 'hold' command accepts items based on their wear flags:
        // - Light objects (Type==ITEM_LIGHT) → WEAR_LIGHT slot (requires only ITEM_TAKE)
        // - Items with Hold OR Wield flag → HOLD slot (legacy: wear_bitvectors[HOLD] = ITEM_HOLD | ITEM_WIELD)
        // - Exception: Two-handed weapons (WieldTwoHanded) cannot be held, must be wielded
        //
        // Note: One-handed weapons CAN be held and go to the HOLD slot (not WIELD slot)
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
        else if (obj.Definition.WearSlots.Contains("WieldTwoHanded") || 
                 obj.Definition.WearSlots.Contains("BothHands"))
        {
            // Legacy act.obj2.c:692-695 - two-handed weapons cannot be held
            return new HoldResult(false, $"You can't hold {obj.Definition.ShortDescription}, wield it instead.");
        }
        else if (obj.Definition.WearSlots.Contains("Hold") || 
                 obj.Definition.WearSlots.Contains("Wield"))
        {
            // Items with Hold OR Wield flag go to Hold slot
            // Legacy: wear_bitvectors[HOLD] = ITEM_HOLD | ITEM_WIELD
            slot = EquipmentSlot.Hold;
        }
        else
        {
            // Item cannot be held
            return new HoldResult(false, $"{obj.Definition.ShortDescription} cannot be held.");
        }

        // Check if slot is already occupied (legacy: act.obj2.c:680-683)
        // If occupied, show "You're already holding $p." or "You're already using $p as light source."
        var equipment = _worldState.GetPlayerEquipment(player);
        if (equipment.TryGetValue(slot, out var alreadyEquipped))
        {
            // Return the already equipped object so CommandHandler can use ActMessage
            return new HoldResult(false, string.Empty, AlreadyEquipped: alreadyEquipped.Definition);
        }

        // Try to equip the object
        if (_worldState.EquipObject(player, obj.InstanceId, slot))
        {
            return new HoldResult(true, string.Empty, obj.Definition);
        }
        else
        {
            // This shouldn't happen since we checked above, but handle gracefully
            return new HoldResult(false, "You can't hold that right now.");
        }
    }
}

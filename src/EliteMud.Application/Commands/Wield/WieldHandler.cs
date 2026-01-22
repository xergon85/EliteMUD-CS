using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Wield;

public sealed record WieldResult(bool Success, string Message, ObjectDefinition? Object = null, ObjectDefinition? AlreadyEquipped = null);

public sealed class WieldHandler
{
    private readonly IWorldState _worldState;

    public WieldHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public WieldResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new WieldResult(false, "Wield what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory using indexed targeting (e.g., "2.sword")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        var obj = TargetParser.FindObject(inventory, target);
        
        if (obj == null)
        {
            return new WieldResult(false, "You don't have that.");
        }

        // Check if object can be wielded
        if (!obj.Definition.WearSlots.Contains("Wield") && 
            !obj.Definition.WearSlots.Contains("WieldTwoHanded") &&
            !obj.Definition.WearSlots.Contains("BothHands"))
        {
            return new WieldResult(false, $"{obj.Definition.ShortDescription} cannot be wielded.");
        }

        // Determine wield slot (prefer Wield over two-handed)
        var slot = obj.Definition.WearSlots.Contains("Wield") 
            ? EquipmentSlot.Wield 
            : EquipmentSlot.BothHands;

        // Check if slot is already occupied
        // Legacy: act.obj2.c:680-683 shows "You're already wielding $p."
        var equipment = _worldState.GetPlayerEquipment(player);
        if (equipment.TryGetValue(slot, out var alreadyEquipped))
        {
            return new WieldResult(false, string.Empty, AlreadyEquipped: alreadyEquipped.Definition);
        }

        // Try to equip the object
        if (_worldState.EquipObject(player, obj.InstanceId, slot))
        {
            return new WieldResult(true, string.Empty, obj.Definition);
        }
        else
        {
            return new WieldResult(false, "You can't wield that right now.");
        }
    }
}

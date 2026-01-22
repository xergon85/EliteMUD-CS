using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Remove;

public sealed record RemoveResult(bool Success, string Message, ObjectDefinition? Object = null);

public sealed class RemoveHandler
{
    private readonly IWorldState _worldState;

    public RemoveHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public RemoveResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new RemoveResult(false, "Remove what?");
        }

        var equipment = _worldState.GetPlayerEquipment(player);

        // Find matching object in equipment using indexed targeting (e.g., "2.ring")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        // Equipment returns ObjectInstances, so we can use FindObject helper
        var equipmentObjects = equipment.Select(kvp => kvp.Value).ToList();
        var obj = TargetParser.FindObject(equipmentObjects, target);
        
        if (obj == null)
        {
            return new RemoveResult(false, "You're not wearing that.");
        }

        // Find the slot this object is in
        var slot = equipment.FirstOrDefault(kvp => kvp.Value.InstanceId == obj.InstanceId).Key;

        // Try to unequip the object
        if (_worldState.UnequipObject(player, slot))
        {
            return new RemoveResult(true, string.Empty, obj.Definition);
        }
        else
        {
            return new RemoveResult(false, "You can't remove that.");
        }
    }
}

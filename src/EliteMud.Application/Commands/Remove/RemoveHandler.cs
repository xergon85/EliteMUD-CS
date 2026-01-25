using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Remove;

public sealed record RemoveResult(bool Success, string Message, ObjectDefinition? Object = null, List<ObjectDefinition>? Objects = null);

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

        // Handle "remove all" - remove all equipped items
        if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (equipment.Count == 0)
            {
                return new RemoveResult(false, "You're not wearing anything.");
            }

            var removedObjects = new List<ObjectDefinition>();
            foreach (var (slot, obj) in equipment.ToList())
            {
                if (_worldState.UnequipObject(player, slot))
                {
                    removedObjects.Add(obj.Definition);
                }
            }

            if (removedObjects.Count == 0)
            {
                return new RemoveResult(false, "You can't seem to remove anything.");
            }

            return new RemoveResult(true, string.Empty, Objects: removedObjects);
        }

        // Parse targeting (e.g., "2.ring" or "all.ring")
        var (index, name) = TargetParser.ParseTarget(target);
        
        if (index == 0)
        {
            return new RemoveResult(false, $"Invalid target: {target}");
        }

        var equipmentObjects = equipment.Select(kvp => kvp.Value).ToList();

        // Handle "remove all.item" - remove all matching equipped items
        if (index == -1)
        {
            var matchingItems = TargetParser.FindAllMatches(equipmentObjects, name);
            if (matchingItems.Count == 0)
            {
                return new RemoveResult(false, $"You're not wearing any {name}.");
            }

            var removedObjects = new List<ObjectDefinition>();
            foreach (var obj in matchingItems)
            {
                var slot = equipment.FirstOrDefault(kvp => kvp.Value.InstanceId == obj.InstanceId).Key;
                if (_worldState.UnequipObject(player, slot))
                {
                    removedObjects.Add(obj.Definition);
                }
            }

            if (removedObjects.Count == 0)
            {
                return new RemoveResult(false, $"You can't remove any {name}.");
            }

            return new RemoveResult(true, string.Empty, Objects: removedObjects);
        }

        // Find specific Nth matching object in equipment
        var targetObj = TargetParser.FindNthMatch(equipmentObjects, name, index);
        
        if (targetObj == null)
        {
            return new RemoveResult(false, "You're not wearing that.");
        }

        // Find the slot this object is in
        var targetSlot = equipment.FirstOrDefault(kvp => kvp.Value.InstanceId == targetObj.InstanceId).Key;

        // Try to unequip the object
        if (_worldState.UnequipObject(player, targetSlot))
        {
            return new RemoveResult(true, string.Empty, targetObj.Definition);
        }
        else
        {
            return new RemoveResult(false, "You can't remove that.");
        }
    }
}

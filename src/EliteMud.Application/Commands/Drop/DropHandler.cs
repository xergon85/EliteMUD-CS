using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Drop;

public sealed record DropResult(bool Success, string Message, ObjectDefinition? Object = null, List<ObjectDefinition>? Objects = null);

public sealed class DropHandler
{
    private readonly IWorldState _worldState;

    public DropHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public DropResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new DropResult(false, "Drop what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Handle "drop all" - drop all items from inventory
        if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (inventory.Count == 0)
            {
                return new DropResult(false, "You aren't carrying anything.");
            }

            var droppedObjects = new List<ObjectDefinition>();
            foreach (var obj in inventory.ToList())
            {
                if (_worldState.DropObject(player, obj.InstanceId))
                {
                    droppedObjects.Add(obj.Definition);
                }
            }

            if (droppedObjects.Count == 0)
            {
                return new DropResult(false, "You can't seem to drop anything.");
            }

            return new DropResult(true, string.Empty, Objects: droppedObjects);
        }

        // Parse indexed targeting (e.g., "2.sword" or "all.sword")
        var (index, name) = TargetParser.ParseTarget(target);

        if (index == 0)
        {
            return new DropResult(false, $"Invalid target: {target}");
        }

        // Handle "drop all.item" - drop all matching items
        if (index == -1)
        {
            var matchingItems = TargetParser.FindAllMatches(inventory, name);
            if (matchingItems.Count == 0)
            {
                return new DropResult(false, $"You don't have any {name}.");
            }

            var droppedObjects = new List<ObjectDefinition>();
            foreach (var obj in matchingItems)
            {
                if (_worldState.DropObject(player, obj.InstanceId))
                {
                    droppedObjects.Add(obj.Definition);
                }
            }

            if (droppedObjects.Count == 0)
            {
                return new DropResult(false, $"You can't drop any {name}.");
            }

            return new DropResult(true, string.Empty, Objects: droppedObjects);
        }

        // Find specific Nth matching object in inventory
        var targetObj = TargetParser.FindNthMatch(inventory, name, index);
        
        if (targetObj == null)
        {
            return new DropResult(false, "You don't have that.");
        }

        // Try to drop the object
        if (_worldState.DropObject(player, targetObj.InstanceId))
        {
            return new DropResult(true, string.Empty, targetObj.Definition);
        }
        else
        {
            return new DropResult(false, "You can't drop that.");
        }
    }
}

using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Drop;

public sealed record DropResult(bool Success, string Message, ObjectDefinition? Object = null);

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

        // Find matching object in inventory using indexed targeting (e.g., "2.sword")
        // Legacy: handler.c:1020-1040 (get_obj_in_list with get_number)
        var obj = TargetParser.FindObject(inventory, target);
        
        if (obj == null)
        {
            return new DropResult(false, "You don't have that.");
        }

        // Try to drop the object
        if (_worldState.DropObject(player, obj.InstanceId))
        {
            return new DropResult(true, string.Empty, obj.Definition);
        }
        else
        {
            return new DropResult(false, "You can't drop that.");
        }
    }
}

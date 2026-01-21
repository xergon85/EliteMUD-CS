using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Inventory;

public sealed record InventoryResult(IReadOnlyList<string> Items);

public sealed class InventoryHandler
{
    private readonly IWorldState _worldState;

    public InventoryHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public InventoryResult Handle(PlayerState player)
    {
        var inventory = _worldState.GetPlayerInventory(player);
        
        if (inventory.Count == 0)
        {
            return new InventoryResult(new List<string> { "You are not carrying anything." });
        }

        var items = new List<string> { "You are carrying:" };
        foreach (var obj in inventory)
        {
            var description = obj.Definition.ShortDescription?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(description))
            {
                items.Add($"  {description}");
            }
        }

        return new InventoryResult(items);
    }
}

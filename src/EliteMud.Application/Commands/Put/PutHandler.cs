using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Put;

public sealed record PutResult(bool Success, string Message);

public sealed class PutHandler
{
    private readonly IWorldState _worldState;

    public PutHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    /// <summary>
    /// Handle put command - put an item into a container.
    /// Syntax: put <item> <container>
    /// Legacy: do_put() in act.obj1.c:844-925
    /// </summary>
    public PutResult Handle(PlayerState player, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new PutResult(false, "Put what in what?");
        }

        // Parse arguments: "put <item> <container>"
        var parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
        {
            return new PutResult(false, "Put what in what?");
        }

        var itemName = parts[0];
        var containerName = parts[1];

        // Find the item in player's inventory
        var item = FindItemInInventory(player, itemName);
        if (item is null)
        {
            return new PutResult(false, $"You don't have {GetArticle(itemName)} {itemName}.");
        }

        // Find the container in inventory or room
        var container = FindContainer(player, containerName);
        if (container is null)
        {
            return new PutResult(false, $"You don't see {GetArticle(containerName)} {containerName} here.");
        }

        // Check if it's actually a container
        if (container.Definition.Details?.Container is null)
        {
            return new PutResult(false, $"The {container.Definition.ShortDescription} is not a container.");
        }

        var containerDetails = container.Definition.Details.Container;

        // Check if container is closeable and closed
        if (containerDetails.Flags.Contains("Closeable", StringComparer.OrdinalIgnoreCase) && container.IsClosed)
        {
            return new PutResult(false, $"The {container.Definition.ShortDescription} is closed.");
        }

        // Check if putting item into itself
        if (item.InstanceId == container.InstanceId)
        {
            return new PutResult(false, "You can't put something inside of itself.");
        }

        // Check container capacity (weight limit)
        // Legacy: GET_OBJ_WEIGHT(obj) calculation includes contents
        int containerCurrentWeight = CalculateWeight(container);
        int itemWeight = item.Definition.Weight;
        int capacity = containerDetails.Capacity;

        if (capacity > 0 && (containerCurrentWeight + itemWeight) > capacity)
        {
            return new PutResult(false, $"The {container.Definition.ShortDescription} is full.");
        }

        // Remove from player inventory and add to container
        if (!player.RemoveFromInventory(item.InstanceId))
        {
            return new PutResult(false, "You can't put that away.");
        }

        container.AddItem(item);
        
        return new PutResult(
            true, 
            $"You put {item.Definition.ShortDescription} in {container.Definition.ShortDescription}.");
    }

    /// <summary>
    /// Find an item in the player's inventory.
    /// Supports indexed targeting (e.g., "2.sword" for second sword).
    /// Legacy: generic_find() with FIND_OBJ_INV
    /// </summary>
    private ObjectInstance? FindItemInInventory(PlayerState player, string itemName)
    {
        var (index, name) = TargetParser.ParseTarget(itemName);
        if (index == 0)
            return null; // Invalid format

        var inventory = _worldState.GetPlayerInventory(player);
        return TargetParser.FindNthMatch(inventory, name, index);
    }

    /// <summary>
    /// Find a container in the player's inventory or room.
    /// Supports indexed targeting (e.g., "2.bag" for second bag).
    /// Searches recursively including items inside containers.
    /// Legacy: generic_find() with FIND_OBJ_INV | FIND_OBJ_ROOM
    /// </summary>
    private ObjectInstance? FindContainer(PlayerState player, string containerName)
    {
        var (index, name) = TargetParser.ParseTarget(containerName);
        if (index == 0)
            return null; // Invalid format

        // Check inventory first (including items inside containers)
        var allItems = _worldState.GetAllPlayerItems(player);
        var inventoryMatch = TargetParser.FindNthMatch(allItems, name, index);
        if (inventoryMatch != null)
            return inventoryMatch;

        // Check room
        var room = _worldState.World.GetRoom(player.RoomId);
        var roomObjects = _worldState.GetObjectsInRoom(room.Id);
        return TargetParser.FindNthMatch(roomObjects, name, index);
    }

    /// <summary>
    /// Calculate total weight of a container including its contents.
    /// Legacy: GET_OBJ_WEIGHT(obj) in utils.h - recursively adds contents
    /// </summary>
    private static int CalculateWeight(ObjectInstance obj)
    {
        int totalWeight = obj.Definition.Weight;
        
        foreach (var item in obj.Contents)
        {
            totalWeight += CalculateWeight(item);
        }
        
        return totalWeight;
    }

    private static string GetArticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "a";
        char first = char.ToLower(word[0]);
        return (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u') ? "an" : "a";
    }
}

using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Put;

public sealed record PutResult(bool Success, string Message, List<ObjectDefinition>? Objects = null, string? ContainerName = null);

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
    /// Supports: put all <container>, put all.item <container>
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

        // Find the container first (needed for all patterns)
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

        // Handle "put all <container>" - put all items into container
        if (itemName.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return PutAllIntoContainer(player, container, null);
        }

        // Parse item targeting
        var (index, name) = TargetParser.ParseTarget(itemName);
        
        if (index == 0)
        {
            return new PutResult(false, $"Invalid target: {itemName}");
        }

        // Handle "put all.item <container>" - put all matching items into container
        if (index == -1)
        {
            return PutAllIntoContainer(player, container, name);
        }

        // Find specific Nth item in inventory
        var inventory = _worldState.GetPlayerInventory(player);
        var item = TargetParser.FindNthMatch(inventory, name, index);
        
        if (item is null)
        {
            return new PutResult(false, $"You don't have {GetArticle(name)} {name}.");
        }

        // Check if putting item into itself
        if (item.InstanceId == container.InstanceId)
        {
            return new PutResult(false, "You can't put something inside of itself.");
        }

        // Check container capacity (weight limit)
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
    /// Put all items (or all matching items) into a container.
    /// </summary>
    /// <param name="player">The player</param>
    /// <param name="container">The target container</param>
    /// <param name="itemFilter">If specified, only put items matching this name. If null, put all items.</param>
    private PutResult PutAllIntoContainer(PlayerState player, ObjectInstance container, string? itemFilter)
    {
        var inventory = _worldState.GetPlayerInventory(player);
        
        // Get items to put (either all or matching filter)
        var itemsToPut = itemFilter == null 
            ? inventory.ToList() 
            : TargetParser.FindAllMatches(inventory, itemFilter);

        if (itemsToPut.Count == 0)
        {
            var message = itemFilter == null 
                ? "You aren't carrying anything." 
                : $"You don't have any {itemFilter}.";
            return new PutResult(false, message);
        }

        var containerDetails = container.Definition.Details!.Container!;
        var putObjects = new List<ObjectDefinition>();

        foreach (var item in itemsToPut)
        {
            // Skip putting container into itself
            if (item.InstanceId == container.InstanceId)
                continue;

            // Check capacity for each item
            int containerCurrentWeight = CalculateWeight(container);
            int itemWeight = item.Definition.Weight;
            int capacity = containerDetails.Capacity;

            if (capacity > 0 && (containerCurrentWeight + itemWeight) > capacity)
            {
                // Container is full, stop adding more
                break;
            }

            // Try to put the item
            if (player.RemoveFromInventory(item.InstanceId))
            {
                container.AddItem(item);
                putObjects.Add(item.Definition);
            }
        }

        if (putObjects.Count == 0)
        {
            var message = itemFilter == null 
                ? "You can't put anything in there." 
                : $"You can't put any {itemFilter} in there.";
            return new PutResult(false, message);
        }

        return new PutResult(true, string.Empty, Objects: putObjects, ContainerName: container.Definition.ShortDescription);
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

using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Get;

public sealed record GetResult(bool Success, string Message, ObjectDefinition? Object = null, string? ContainerName = null, List<ObjectDefinition>? Objects = null);

public sealed class GetHandler
{
    private readonly IWorldState _worldState;

    public GetHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    /// <summary>
    /// Handle get command - supports both "get <item>" and "get <item> <container>"
    /// Legacy: do_get() and get_from_container() in act.obj1.c:761-842, 649-724
    /// </summary>
    public GetResult Handle(PlayerState player, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new GetResult(false, "Get what?");
        }

        // Parse arguments: "get <item>" or "get <item> <container>"
        var parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        var itemName = parts[0];
        var containerName = parts.Length > 1 ? parts[1] : null;

        // If container specified, get from container
        if (!string.IsNullOrWhiteSpace(containerName))
        {
            return GetFromContainer(player, itemName, containerName);
        }

        // Otherwise, get from room
        return GetFromRoom(player, itemName);
    }

    /// <summary>
    /// Get an item from the room.
    /// Legacy: get_from_room() in act.obj1.c:727-757
    /// </summary>
    private GetResult GetFromRoom(PlayerState player, string target)
    {
        var room = _worldState.World.GetRoom(player.RoomId);
        var objects = _worldState.GetObjectsInRoom(room.Id);

        // Handle "get all" - get all objects from room
        if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var takenObjects = new List<ObjectDefinition>();
            foreach (var obj in objects.ToList())
            {
                if (_worldState.TakeObject(player, obj.InstanceId))
                {
                    takenObjects.Add(obj.Definition);
                }
            }
            
            if (takenObjects.Count == 0)
            {
                return new GetResult(false, "There doesn't seem to be anything you can get here.");
            }
            
            // Return the list of objects so CommandHandler can echo each individually
            return new GetResult(true, string.Empty, Objects: takenObjects);
        }

        // Find matching object
        foreach (var obj in objects)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                // Try to take the object
                if (_worldState.TakeObject(player, obj.InstanceId))
                {
                    return new GetResult(true, string.Empty, obj.Definition);
                }
                else
                {
                    return new GetResult(false, "You can't take that.");
                }
            }
        }

        return new GetResult(false, $"You don't see {GetArticle(target)} {target} here.");
    }

    /// <summary>
    /// Get an item from a container (corpse, bag, etc.)
    /// Legacy: get_from_container() in act.obj1.c:649-724
    /// </summary>
    private GetResult GetFromContainer(PlayerState player, string itemName, string containerName)
    {
        // Parse container name to check for "all.X" pattern
        var (containerIndex, containerTargetName) = TargetParser.ParseTarget(containerName);
        
        if (containerIndex == 0)
        {
            return new GetResult(false, $"Invalid container target: {containerName}");
        }
        
        // Handle "get <item> all.container" or "get all.item all.container"
        if (containerIndex == -1)
        {
            return GetFromAllContainers(player, itemName, containerTargetName);
        }
        
        // Find specific container
        var container = FindContainer(player, containerName);
        
        if (container is null)
        {
            return new GetResult(false, $"You don't have {GetArticle(containerName)} {containerName}.");
        }

        // Check if it's actually a container
        if (!container.Definition.Type.Equals("container", StringComparison.OrdinalIgnoreCase))
        {
            return new GetResult(false, $"The {container.Definition.ShortDescription} is not a container.");
        }

        var containerDetails = container.Definition.Details?.Container;
        
        // Check if container is closed (only if it's closeable and not a corpse)
        // Corpses are always open (CorpseType > 0 means it's a corpse)
        bool isCorpse = containerDetails?.CorpseType > 0;
        if (!isCorpse && containerDetails?.Flags.Contains("Closeable", StringComparer.OrdinalIgnoreCase) == true && container.IsClosed)
        {
            return new GetResult(false, $"The {container.Definition.ShortDescription} is closed.");
        }
        
        // Handle "get all <container>"
        if (itemName.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return GetAllFromContainer(player, container);
        }

        // Parse indexed targeting (e.g., "2.wheat" for second wheat in container)
        var (index, name) = TargetParser.ParseTarget(itemName);
        
        if (index == 0)
        {
            return new GetResult(false, $"Invalid target: {itemName}");
        }
        
        // Support "get all.wheat container" pattern
        if (index == -1)
        {
            var matchingItems = TargetParser.FindAllMatches(container.Contents, name);
            if (matchingItems.Count == 0)
            {
                return new GetResult(false, $"There doesn't seem to be any {name} in the {container.Definition.ShortDescription}.");
            }
            
            var takenObjects = new List<ObjectDefinition>();
            foreach (var matchingItem in matchingItems)
            {
                if (container.RemoveItem(matchingItem))
                {
                    player.AddToInventory(matchingItem.InstanceId);
                    takenObjects.Add(matchingItem.Definition);
                }
            }
            
            if (takenObjects.Count == 0)
            {
                return new GetResult(false, $"You can't get any {name} from the {container.Definition.ShortDescription}.");
            }
            
            return new GetResult(true, string.Empty, Objects: takenObjects, ContainerName: container.Definition.ShortDescription);
        }

        // Find the Nth matching item in container
        var targetItem = TargetParser.FindNthMatch(container.Contents, name, index);
        
        if (targetItem == null)
        {
            return new GetResult(false, $"There doesn't seem to be {GetArticle(name)} {index}.{name} in the {container.Definition.ShortDescription}.");
        }

        // Remove from container and add to player inventory
        if (container.RemoveItem(targetItem))
        {
            player.AddToInventory(targetItem.InstanceId);
            return new GetResult(true, string.Empty, targetItem.Definition, container.Definition.ShortDescription);
        }
        else
        {
            return new GetResult(false, "You can't take that.");
        }
    }

    /// <summary>
    /// Get items from all matching containers (e.g., "get all.wheat all.corpse").
    /// </summary>
    private GetResult GetFromAllContainers(PlayerState player, string itemName, string containerTargetName)
    {
        // Find all matching containers in inventory and room
        var allItems = _worldState.GetAllPlayerItems(player);
        var room = _worldState.World.GetRoom(player.RoomId);
        var roomObjects = _worldState.GetObjectsInRoom(room.Id);
        
        var allPotentialContainers = allItems.Concat(roomObjects).ToList();
        var matchingContainers = TargetParser.FindAllMatches(allPotentialContainers, containerTargetName)
            .Where(obj => obj.Definition.Type.Equals("container", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (matchingContainers.Count == 0)
        {
            return new GetResult(false, $"You don't see any {containerTargetName} here.");
        }
        
        var takenObjects = new List<ObjectDefinition>();
        
        foreach (var container in matchingContainers)
        {
            var containerDetails = container.Definition.Details?.Container;
            
            // Skip closed containers
            bool isCorpse = containerDetails?.CorpseType > 0;
            if (!isCorpse && containerDetails?.Flags.Contains("Closeable", StringComparer.OrdinalIgnoreCase) == true && container.IsClosed)
            {
                continue;
            }
            
            // Get items from this container
            if (itemName.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Get all items from container
                foreach (var item in container.Contents.ToList())
                {
                    if (container.RemoveItem(item))
                    {
                        player.AddToInventory(item.InstanceId);
                        takenObjects.Add(item.Definition);
                    }
                }
            }
            else
            {
                // Get all matching items from container
                var (index, name) = TargetParser.ParseTarget(itemName);
                
                if (index == -1)
                {
                    // "all.wheat" pattern
                    var matchingItems = TargetParser.FindAllMatches(container.Contents, name);
                    foreach (var item in matchingItems)
                    {
                        if (container.RemoveItem(item))
                        {
                            player.AddToInventory(item.InstanceId);
                            takenObjects.Add(item.Definition);
                        }
                    }
                }
                else if (index > 0)
                {
                    // "2.wheat" pattern - get specific item from this container
                    var item = TargetParser.FindNthMatch(container.Contents, name, index);
                    if (item != null && container.RemoveItem(item))
                    {
                        player.AddToInventory(item.InstanceId);
                        takenObjects.Add(item.Definition);
                    }
                }
            }
        }
        
        if (takenObjects.Count == 0)
        {
            return new GetResult(false, $"You don't find anything to get from the {containerTargetName}.");
        }
        
        return new GetResult(true, string.Empty, Objects: takenObjects);
    }

    /// <summary>
    /// Get all items from a container.
    /// Legacy: MODE_GET_ALL_CONT in act.obj1.c:661-678
    /// </summary>
    private GetResult GetAllFromContainer(PlayerState player, ObjectInstance container)
    {
        if (container.Contents.Count == 0)
        {
            return new GetResult(false, $"The {container.Definition.ShortDescription} is empty.");
        }

        var takenObjects = new List<ObjectDefinition>();
        foreach (var item in container.Contents.ToList())
        {
            if (container.RemoveItem(item))
            {
                player.AddToInventory(item.InstanceId);
                takenObjects.Add(item.Definition);
            }
        }

        if (takenObjects.Count == 0)
        {
            return new GetResult(false, $"The {container.Definition.ShortDescription} doesn't contain anything you can get.");
        }

        // Check if looting another player's corpse (value[3]=2 for player corpse)
        bool isPlayerCorpse = container.Definition.Values.Count > 3 && container.Definition.Values[3] == 2;
        if (isPlayerCorpse && !container.Definition.ShortDescription.Contains(player.Name, StringComparison.OrdinalIgnoreCase))
        {
            // Log looting (legacy: fight.c:672-673)
            Console.WriteLine($"[LOOT] {player.Name} looting {container.Definition.ShortDescription}.");
        }

        // Create summary message for multiple items
        string summaryMessage = takenObjects.Count == 1 
            ? "1 item" 
            : $"{takenObjects.Count} items";

        // Return the list of objects with container name so CommandHandler can echo each individually
        return new GetResult(true, summaryMessage, Objects: takenObjects, ContainerName: container.Definition.ShortDescription);
    }

    /// <summary>
    /// Find a container in the player's inventory or room.
    /// Supports indexed targeting (e.g., "2.corpse" for second corpse).
    /// Legacy: generic_find() with FIND_OBJ_INV | FIND_OBJ_ROOM, get_number() in handler.c:997-1016
    /// </summary>
    private ObjectInstance? FindContainer(PlayerState player, string containerName)
    {
        // Parse "2.corpse" style targeting
        var (index, name) = TargetParser.ParseTarget(containerName);
        if (index == 0)
            return null; // Invalid format (e.g., "abc.corpse")
        
        // TODO: Support "all.X" pattern (index == -1) by handling at CommandHandler level
        //       Legacy does this by calling get_from_container for each matching container
        if (index == -1)
            return null; // Not supported yet
        
        // Check inventory first (including nested items in containers)
        var allItems = _worldState.GetAllPlayerItems(player);
        var inventoryMatch = TargetParser.FindNthMatch(allItems, name, index);
        if (inventoryMatch != null)
            return inventoryMatch;

        // Check room
        var room = _worldState.World.GetRoom(player.RoomId);
        var roomObjects = _worldState.GetObjectsInRoom(room.Id);
        return TargetParser.FindNthMatch(roomObjects, name, index);
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        
        // Check if target matches any keyword in the object name
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }

    private static string GetArticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "a";
        char first = char.ToLower(word[0]);
        return (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u') ? "an" : "a";
    }
}

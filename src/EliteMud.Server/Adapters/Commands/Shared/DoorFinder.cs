using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Server.Adapters.Commands.Shared;

/// <summary>
/// Result of door search operation.
/// Legacy: find_door() return value in act.movement.c
/// </summary>
public sealed record DoorFindResult(
    bool Found,
    Direction? Direction,
    ExitDefinition? Exit,
    string? ErrorMessage);

/// <summary>
/// Utility for finding doors by keyword and direction.
/// Legacy: find_door() function in act.movement.c:372-419
/// </summary>
public static class DoorFinder
{
    /// <summary>
    /// Find a door given a target keyword and optional direction.
    /// Examples:
    /// - "gate" - searches all directions for door with "gate" keyword
    /// - "north" - finds door to the north (any keyword)
    /// - "gate north" or "north gate" - finds north door with "gate" keyword
    /// 
    /// Legacy behavior from find_door():
    /// - If direction specified, check if exit exists and keyword matches
    /// - If no direction, scan all directions for keyword match
    /// </summary>
    public static DoorFindResult FindDoor(IWorldState worldState, PlayerState player, string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return new DoorFindResult(false, null, null, "Open what?");
        }

        // Parse argument into type (keyword) and dir (direction)
        var parts = argument.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        string? typeKeyword = null;
        string? dirKeyword = null;
        
        if (parts.Length == 1)
        {
            // Could be either a direction or a door keyword
            // Try direction first, then keyword
            if (TryParseDirection(parts[0], out var _))
            {
                dirKeyword = parts[0];
            }
            else
            {
                typeKeyword = parts[0];
            }
        }
        else if (parts.Length >= 2)
        {
            // Try to identify which part is the direction
            if (TryParseDirection(parts[0], out var _))
            {
                dirKeyword = parts[0];
                typeKeyword = parts[1];
            }
            else if (TryParseDirection(parts[1], out var _))
            {
                typeKeyword = parts[0];
                dirKeyword = parts[1];
            }
            else
            {
                // Neither is a direction, treat as multi-word keyword
                typeKeyword = string.Join(" ", parts);
            }
        }

        var room = worldState.World.GetRoom(player.RoomId);
        
        // If direction was specified, search that specific direction
        if (dirKeyword != null && TryParseDirection(dirKeyword, out var direction))
        {
            var exit = room.Exits.FirstOrDefault(e => e.Direction == direction);
            
            if (exit == null)
            {
                return new DoorFindResult(false, null, null, "I really don't see how you can close anything there.");
            }
            
            // If a keyword was also specified, verify it matches
            if (typeKeyword != null)
            {
                if (exit.Keywords == null || !exit.Keywords.Any(k => IsName(typeKeyword, k)))
                {
                    return new DoorFindResult(false, null, null, $"I see no {typeKeyword} there.");
                }
            }
            
            return new DoorFindResult(true, direction, exit, null);
        }
        
        // No direction specified - search all directions for keyword match
        if (typeKeyword != null)
        {
            foreach (var exit in room.Exits)
            {
                if (exit.IsDoor && exit.Keywords != null)
                {
                    if (exit.Keywords.Any(k => IsName(typeKeyword, k)))
                    {
                        return new DoorFindResult(true, exit.Direction, exit, null);
                    }
                }
            }
            
            return new DoorFindResult(false, null, null, $"I see no {typeKeyword} here.");
        }
        
        return new DoorFindResult(false, null, null, "Open what?");
    }
    
    /// <summary>
    /// Parse direction from string.
    /// </summary>
    private static bool TryParseDirection(string input, out Direction direction)
    {
        switch (input.Trim().ToLowerInvariant())
        {
            case "north":
            case "n":
                direction = Direction.North;
                return true;
            case "east":
            case "e":
                direction = Direction.East;
                return true;
            case "south":
            case "s":
                direction = Direction.South;
                return true;
            case "west":
            case "w":
                direction = Direction.West;
                return true;
            case "up":
            case "u":
                direction = Direction.Up;
                return true;
            case "down":
            case "d":
                direction = Direction.Down;
                return true;
            default:
                direction = Direction.North;
                return false;
        }
    }
    
    /// <summary>
    /// Check if a search string matches any keyword (partial match, space-separated).
    /// Legacy: isname() function in handler.c
    /// </summary>
    private static bool IsName(string searchStr, string keywords)
    {
        var search = searchStr.Trim().ToLowerInvariant();
        var keywordList = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        return keywordList.Any(keyword => keyword.ToLowerInvariant().StartsWith(search));
    }
}

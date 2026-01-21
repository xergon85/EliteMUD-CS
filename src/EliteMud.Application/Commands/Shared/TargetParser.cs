using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Provides utilities for parsing indexed targeting syntax (e.g., "2.corpse").
/// Legacy reference: handler.c:997-1040 (get_number, get_obj_in_list)
/// </summary>
public static class TargetParser
{
    /// <summary>
    /// Parse "2.corpse" style targeting.
    /// Returns (index, name) where index is 1-based.
    /// Legacy reference: get_number() in handler.c:997-1016
    /// </summary>
    /// <param name="input">The target string (e.g., "2.corpse", "corpse", "all.corpse")</param>
    /// <returns>
    /// A tuple of (Index, Name) where:
    /// - Index = 1 for "corpse" (default to first match)
    /// - Index = 2 for "2.corpse" (second match)
    /// - Index = 0 for invalid format (e.g., "abc.corpse")
    /// - Index = -1 for "all.corpse" (special case for all matches)
    /// </returns>
    public static (int Index, string Name) ParseTarget(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (1, input ?? string.Empty);
        
        var dotIndex = input.IndexOf('.');
        if (dotIndex <= 0)
            return (1, input); // No dot = first match
        
        var numberPart = input.Substring(0, dotIndex);
        var namePart = input.Substring(dotIndex + 1);
        
        // Check for "all.X" special case
        if (numberPart.Equals("all", StringComparison.OrdinalIgnoreCase))
            return (-1, namePart);
        
        // Check if number part is all digits
        if (!numberPart.All(char.IsDigit))
            return (0, input); // Invalid format
        
        int index = int.Parse(numberPart);
        return (index, namePart);
    }
    
    /// <summary>
    /// Find the Nth matching object in a list.
    /// Legacy reference: get_obj_in_list() in handler.c:1020-1040
    /// </summary>
    /// <param name="objects">The list of objects to search</param>
    /// <param name="name">The name/keyword to match</param>
    /// <param name="index">The 1-based index of the match to return (1 = first match, 2 = second, etc.)</param>
    /// <returns>The Nth matching object, or null if not found</returns>
    public static ObjectInstance? FindNthMatch(
        IEnumerable<ObjectInstance> objects, 
        string name, 
        int index)
    {
        if (index < 1)
            return null; // Invalid index
        
        int matchCount = 0;
        foreach (var obj in objects)
        {
            if (MatchesTarget(obj.Definition, name))
            {
                matchCount++;
                if (matchCount == index)
                    return obj;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Find all matching objects in a list.
    /// Used for "all.X" pattern support.
    /// </summary>
    /// <param name="objects">The list of objects to search</param>
    /// <param name="name">The name/keyword to match</param>
    /// <returns>All matching objects</returns>
    public static List<ObjectInstance> FindAllMatches(
        IEnumerable<ObjectInstance> objects, 
        string name)
    {
        var matches = new List<ObjectInstance>();
        foreach (var obj in objects)
        {
            if (MatchesTarget(obj.Definition, name))
            {
                matches.Add(obj);
            }
        }
        return matches;
    }
    
    /// <summary>
    /// High-level helper: Parse target string and find object in one call.
    /// This is the common pattern used by most commands.
    /// </summary>
    /// <param name="objects">The list of objects to search</param>
    /// <param name="targetString">The target string (e.g., "sword", "2.sword")</param>
    /// <returns>The matching object, or null if not found or invalid format</returns>
    public static ObjectInstance? FindObject(
        IEnumerable<ObjectInstance> objects,
        string targetString)
    {
        var (index, name) = ParseTarget(targetString);
        if (index == 0 || index == -1)
            return null; // Invalid format or "all.X" pattern (not supported by this method)
        
        return FindNthMatch(objects, name, index);
    }
    
    /// <summary>
    /// High-level helper: Parse target string and find mob in one call.
    /// This is the common pattern used by most commands.
    /// </summary>
    /// <param name="mobs">The list of mobs to search</param>
    /// <param name="targetString">The target string (e.g., "guard", "2.guard")</param>
    /// <returns>The matching mob, or null if not found or invalid format</returns>
    public static MobInstance? FindMob(
        IEnumerable<MobInstance> mobs,
        string targetString)
    {
        var (index, name) = ParseTarget(targetString);
        if (index == 0 || index == -1)
            return null; // Invalid format or "all.X" pattern (not supported by this method)
        
        return FindNthMatch(mobs, name, index);
    }
    
    /// <summary>
    /// Find the Nth matching mob in a list.
    /// Legacy reference: get_char_room_vis() in handler.c:1481-1501
    /// </summary>
    /// <param name="mobs">The list of mobs to search</param>
    /// <param name="name">The name/keyword to match</param>
    /// <param name="index">The 1-based index of the match to return (1 = first match, 2 = second, etc.)</param>
    /// <returns>The Nth matching mob, or null if not found</returns>
    public static MobInstance? FindNthMatch(
        IEnumerable<MobInstance> mobs, 
        string name, 
        int index)
    {
        if (index < 1)
            return null; // Invalid index
        
        int matchCount = 0;
        foreach (var mob in mobs)
        {
            if (MatchesTarget(mob.Definition, name))
            {
                matchCount++;
                if (matchCount == index)
                    return mob;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Check if an object definition matches a target keyword.
    /// Uses the same logic as the original isname() function.
    /// </summary>
    private static bool MatchesTarget(ObjectDefinition objDef, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;
        
        var targetLower = target.ToLowerInvariant();
        var keywords = objDef.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) 
            ?? Array.Empty<string>();
        
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
    
    /// <summary>
    /// Check if a mob definition matches a target keyword.
    /// Uses the same logic as the original isname() function.
    /// </summary>
    private static bool MatchesTarget(MobDefinition mobDef, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;
        
        var targetLower = target.ToLowerInvariant();
        var keywords = mobDef.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) 
            ?? Array.Empty<string>();
        
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
}

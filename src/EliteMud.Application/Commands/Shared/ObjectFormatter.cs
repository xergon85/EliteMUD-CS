using EliteMud.Application.World;

namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Utility for formatting object lists with stacking and proper ordering.
/// Legacy-compatible formatting matching EliteMUD's list_obj_to_char.
/// </summary>
public static class ObjectFormatter
{
    /// <summary>
    /// Formats a list of objects with stacking (showing count for duplicates).
    /// Items are sorted with newest first (reverse chronological).
    /// Legacy format: "( 3) item name" or "     item name"
    /// </summary>
    /// <param name="objects">List of objects to format</param>
    /// <param name="indent">Base indentation (default empty, inventory adds its own spacing)</param>
    /// <returns>List of formatted strings, one per unique item</returns>
    public static IReadOnlyList<string> FormatObjectList(IReadOnlyList<ObjectInstance> objects, string indent = "")
    {
        if (objects.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        
        // Reverse the list so newest items appear first (legacy behavior)
        var reversedObjects = objects.Reverse().ToList();
        
        // Group by object definition ID to stack identical items
        var processedIndices = new HashSet<int>();
        
        for (int i = 0; i < reversedObjects.Count; i++)
        {
            if (processedIndices.Contains(i))
                continue;
                
            var obj = reversedObjects[i];
            var description = obj.Definition.ShortDescription?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(description))
                continue;
            
            // Count identical items (same object definition ID and short description)
            int count = 1;
            processedIndices.Add(i);
            
            for (int j = i + 1; j < reversedObjects.Count; j++)
            {
                if (processedIndices.Contains(j))
                    continue;
                    
                // Legacy matching: same item number and short description
                if (reversedObjects[j].Definition.Id == obj.Definition.Id &&
                    reversedObjects[j].Definition.ShortDescription == obj.Definition.ShortDescription)
                {
                    count++;
                    processedIndices.Add(j);
                }
            }
            
            // Format: "( count) description" or "     description"
            // Legacy uses sprintf(buf, "(%2d) ", count) with right-aligned count
            string formatted = count > 1 
                ? $"{indent}({count,2}) {description}"
                : $"{indent}     {description}";
                
            result.Add(formatted);
        }
        
        return result;
    }
}

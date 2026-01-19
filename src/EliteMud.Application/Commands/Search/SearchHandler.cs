using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Search;

public sealed record SearchResult(IReadOnlyList<string> Lines);

public sealed class SearchHandler
{
    private readonly IWorldState _worldState;

    public SearchHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public SearchResult Handle(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResult(new List<string> { "Search for what? Usage: search <item name>" });
        }

        var results = _worldState.SearchObjects(query);

        if (results.Count == 0)
        {
            return new SearchResult(new List<string> { $"No objects found matching '{query}'." });
        }

        var lines = new List<string>();
        
        if (results.Count > 50)
        {
            lines.Add($"Found {results.Count} objects matching '{query}' (showing first 50):");
        }
        else
        {
            lines.Add($"Found {results.Count} object(s) matching '{query}':");
        }

        var count = 0;
        foreach (var obj in results)
        {
            if (count >= 50) break;
            
            var shortDesc = obj.ShortDescription?.Replace("\n", "").Trim() ?? "(no description)";
            var wearInfo = obj.WearSlots.Count > 0 ? $" [{string.Join(", ", obj.WearSlots)}]" : "";
            
            lines.Add($"  {obj.Id}: {shortDesc}{wearInfo}");
            count++;
        }

        lines.Add($"Use 'load <id>' to spawn an object.");

        return new SearchResult(lines);
    }
}

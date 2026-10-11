using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using System.Text;

namespace EliteMud.Application.Commands.Who;

/// <summary>
/// Handler for the 'who' command that displays all players currently online.
/// Matches legacy EliteMUD format:
/// 
/// #rPlayers
/// -------#N
/// [level class] #CName#N title
/// ...
/// X visible characters displayed.
/// </summary>
public sealed class WhoHandler
{
    private readonly IConnectionDirectory _connections;
    private readonly RulesContent _rules;

    public WhoHandler(IConnectionDirectory connections, RulesContent rules)
    {
        _connections = connections;
        _rules = rules;
    }

    public CommandResult Handle()
    {
        var players = _connections.GetPlayers();
        var output = new StringBuilder();

        // Header - red "Players" with separator
        output.AppendLine("#rPlayers");
        output.AppendLine("-------#N");

        // List each player
        foreach (var player in players.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            // Legacy CLASS_ABBR: the class's abbreviation from content, with its colours
            var classAbbr = _rules.Classes.TryGetValue(player.CharacterClass, out var characterClass)
                ? characterClass.Abbreviation
                : player.CharacterClass;
            var title = player.Title ?? "the newbie";
            
            // Format: [level class] #CName#N title
            output.AppendLine($"[#Y{player.Level,2}#N {classAbbr}] #C{player.Name}#N {title}");
        }

        // Footer - player count
        output.AppendLine();
        output.Append($"#N{players.Count} visible characters displayed.");

        return CommandResult.Ok(output.ToString());
    }
}

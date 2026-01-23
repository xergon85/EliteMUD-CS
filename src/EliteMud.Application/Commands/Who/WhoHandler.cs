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

    public WhoHandler(IConnectionDirectory connections)
    {
        _connections = connections;
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
            var classAbbr = GetClassAbbreviation(player.CharacterClass);
            var title = player.Title ?? "the newbie";
            
            // Format: [level class] #CName#N title
            output.AppendLine($"[#Y{player.Level,2}#N {classAbbr}] #C{player.Name}#N {title}");
        }

        // Footer - player count
        output.AppendLine();
        output.Append($"#N{players.Count} visible characters displayed.");

        return CommandResult.Ok(output.ToString());
    }

    /// <summary>
    /// Get 3-character class abbreviation.
    /// Matches legacy CLASS_ABBR macro.
    /// </summary>
    private static string GetClassAbbreviation(string characterClass)
    {
        return characterClass.ToLower() switch
        {
            "magic user" => "Mag",
            "cleric" => "Cle",
            "thief" => "Thi",
            "warrior" => "War",
            "ranger" => "Ran",
            "paladin" => "Pal",
            "rogue" => "Rog",
            "priest" => "Pri",
            "nightblade" => "Nig",
            "battlemage" => "Bat",
            "spellsword" => "Spe",
            "monk" => "Mon",
            "druid" => "Dru",
            "shaman" => "Sha",
            _ => characterClass.Length >= 3 ? characterClass[..3] : characterClass
        };
    }
}

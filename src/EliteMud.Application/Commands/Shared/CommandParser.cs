using EliteMud.Game;

namespace EliteMud.Application.Commands.Shared;

public enum CommandKind
{
    None,
    Quit,
    Look,
    Examine,
    Get,
    Drop,
    Inventory,
    Equipment,
    Wear,
    Remove,
    Wield,
    Hold,
    Load,
    Search,
    Who,
    Score,
    ResetZone,
    Say,
    Move,
    Kill,
    Flee,
    Wimpy,
    ImportLegacy,
    Save,
    Sleep,
    Rest,
    Sit,
    Wake,
    Stand,
    Consider,
    Kick,
    SetSkill,
    SetLevel,
    Skills,
    Unknown
}

/// <summary>
/// Represents a parsed command from user input.
/// Contains both legacy CommandKind (for backward compat) and Verb string (for attribute-based routing).
/// </summary>
public sealed record CommandRequest(
    CommandKind Kind,
    string Verb,
    string? Argument,
    Direction? Direction);

public sealed class CommandParser
{
    public CommandRequest Parse(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return new CommandRequest(CommandKind.None, "", null, null);
        }

        // Extract verb (first word) for attribute-based routing
        var spaceIndex = trimmed.IndexOf(' ');
        var verb = spaceIndex == -1 
            ? trimmed.ToLowerInvariant()
            : trimmed[..spaceIndex].ToLowerInvariant();

        if (trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Quit, verb, null, null);
        }

        if (trimmed.Equals("look", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("l", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Look, verb, null, null);
        }

        if (trimmed.StartsWith("look ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("l ", StringComparison.OrdinalIgnoreCase))
        {
            var targetIndex = trimmed.IndexOf(' ');
            var target = trimmed[(targetIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Look, verb, target, null);
        }

        if (trimmed.StartsWith("examine ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ex ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("exa ", StringComparison.OrdinalIgnoreCase))
        {
            var targetIndex = trimmed.IndexOf(' ');
            var target = trimmed[(targetIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Examine, verb, target, null);
        }

        if (trimmed.StartsWith("get ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("take ", StringComparison.OrdinalIgnoreCase))
        {
            var targetIndex = trimmed.IndexOf(' ');
            var target = trimmed[(targetIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Get, verb, target, null);
        }

        if (trimmed.StartsWith("drop ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Drop, verb, target, null);
        }

        if (trimmed.Equals("inventory", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("inv", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Inventory, verb, null, null);
        }

        if (trimmed.Equals("equipment", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("eq", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Equipment, verb, null, null);
        }

        if (trimmed.StartsWith("wear ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Wear, verb, target, null);
        }

        if (trimmed.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[7..].Trim();
            return new CommandRequest(CommandKind.Remove, verb, target, null);
        }

        if (trimmed.StartsWith("wield ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[6..].Trim();
            return new CommandRequest(CommandKind.Wield, verb, target, null);
        }

        if (trimmed.StartsWith("hold ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Hold, verb, target, null);
        }

        if (trimmed.StartsWith("load ", StringComparison.OrdinalIgnoreCase))
        {
            var objectId = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Load, verb, objectId, null);
        }

        if (trimmed.StartsWith("search ", StringComparison.OrdinalIgnoreCase))
        {
            var query = trimmed[7..].Trim();
            return new CommandRequest(CommandKind.Search, verb, query, null);
        }

        if (trimmed.Equals("who", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Who, verb, null, null);
        }

        if (trimmed.Equals("score", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("sc", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Score, verb, null, null);
        }

        if (trimmed.Equals("zreset", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.ResetZone, verb, null, null);
        }

        if (trimmed.StartsWith("zreset ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("reset ", StringComparison.OrdinalIgnoreCase))
        {
            var idText = trimmed[(trimmed.IndexOf(' ') + 1)..].Trim();
            return new CommandRequest(CommandKind.ResetZone, verb, idText, null);
        }

        if (trimmed.StartsWith("say ", StringComparison.OrdinalIgnoreCase))
        {
            var message = trimmed[4..].Trim();
            return new CommandRequest(CommandKind.Say, verb, message, null);
        }

        if (trimmed.Equals("say", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Say, verb, string.Empty, null);
        }

        if (trimmed.Equals("import-legacy", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.ImportLegacy, verb, null, null);
        }

        if (trimmed.StartsWith("import-legacy ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[14..].Trim();
            return new CommandRequest(CommandKind.ImportLegacy, verb, argument, null);
        }

        if (trimmed.StartsWith("kill ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("k ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("hit ", StringComparison.OrdinalIgnoreCase))
        {
            var targetIndex = trimmed.IndexOf(' ');
            var target = trimmed[(targetIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Kill, verb, target, null);
        }

        if (trimmed.Equals("flee", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("f", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Flee, verb, null, null);
        }

        if (trimmed.Equals("wimpy", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Wimpy, verb, null, null);
        }

        if (trimmed.StartsWith("wimpy ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[6..].Trim();
            return new CommandRequest(CommandKind.Wimpy, verb, argument, null);
        }

        if (trimmed.Equals("save", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Save, verb, null, null);
        }

        if (trimmed.Equals("sleep", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Sleep, verb, null, null);
        }

        if (trimmed.Equals("rest", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Rest, verb, null, null);
        }

        if (trimmed.Equals("sit", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Sit, verb, null, null);
        }

        if (trimmed.Equals("wake", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Wake, verb, null, null);
        }

        if (trimmed.Equals("stand", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("st", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Stand, verb, null, null);
        }

        if (trimmed.StartsWith("consider ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("con ", StringComparison.OrdinalIgnoreCase))
        {
            var targetIndex = trimmed.IndexOf(' ');
            var target = trimmed[(targetIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Consider, verb, target, null);
        }

        if (trimmed.Equals("kick", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Kick, verb, null, null);
        }

        if (trimmed.StartsWith("kick ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Kick, verb, target, null);
        }

        if (trimmed.StartsWith("setskill ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[9..].Trim();
            return new CommandRequest(CommandKind.SetSkill, verb, argument, null);
        }

        if (trimmed.StartsWith("setlevel ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[9..].Trim();
            return new CommandRequest(CommandKind.SetLevel, verb, argument, null);
        }

        if (trimmed.Equals("skills", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("skill", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Skills, verb, null, null);
        }

        if (trimmed.StartsWith("skills ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[7..].Trim();
            return new CommandRequest(CommandKind.Skills, verb, argument, null);
        }

        if (trimmed.StartsWith("skill ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[6..].Trim();
            return new CommandRequest(CommandKind.Skills, verb, argument, null);
        }

        if (TryParseDirection(trimmed, out var direction))
        {
            return new CommandRequest(CommandKind.Move, verb, null, direction);
        }

        if (trimmed.StartsWith("go ", StringComparison.OrdinalIgnoreCase)
            && TryParseDirection(trimmed[3..], out direction))
        {
            return new CommandRequest(CommandKind.Move, "go", null, direction);
        }

        return new CommandRequest(CommandKind.Unknown, verb, null, null);
    }

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
}

using EliteMud.Application.World;
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
    ResetZone,
    Say,
    Move,
    ImportLegacy,
    Unknown
}

public sealed record CommandRequest(CommandKind Kind, string? Argument, Direction? Direction);

public sealed class CommandParser
{
    public CommandRequest Parse(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return new CommandRequest(CommandKind.None, null, null);
        }

        if (trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Quit, null, null);
        }

        if (trimmed.Equals("look", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Look, null, null);
        }

        if (trimmed.StartsWith("look ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Look, target, null);
        }

        if (trimmed.StartsWith("examine ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ex ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("exa ", StringComparison.OrdinalIgnoreCase))
        {
            var spaceIndex = trimmed.IndexOf(' ');
            var target = trimmed[(spaceIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Examine, target, null);
        }

        if (trimmed.StartsWith("get ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("take ", StringComparison.OrdinalIgnoreCase))
        {
            var spaceIndex = trimmed.IndexOf(' ');
            var target = trimmed[(spaceIndex + 1)..].Trim();
            return new CommandRequest(CommandKind.Get, target, null);
        }

        if (trimmed.StartsWith("drop ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Drop, target, null);
        }

        if (trimmed.Equals("inventory", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("inv", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Inventory, null, null);
        }

        if (trimmed.Equals("equipment", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("eq", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Equipment, null, null);
        }

        if (trimmed.StartsWith("wear ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Wear, target, null);
        }

        if (trimmed.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[7..].Trim();
            return new CommandRequest(CommandKind.Remove, target, null);
        }

        if (trimmed.StartsWith("wield ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[6..].Trim();
            return new CommandRequest(CommandKind.Wield, target, null);
        }

        if (trimmed.StartsWith("hold ", StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Hold, target, null);
        }

        if (trimmed.StartsWith("load ", StringComparison.OrdinalIgnoreCase))
        {
            var objectId = trimmed[5..].Trim();
            return new CommandRequest(CommandKind.Load, objectId, null);
        }

        if (trimmed.StartsWith("search ", StringComparison.OrdinalIgnoreCase))
        {
            var query = trimmed[7..].Trim();
            return new CommandRequest(CommandKind.Search, query, null);
        }

        if (trimmed.Equals("who", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Who, null, null);
        }

        if (trimmed.Equals("zreset", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.ResetZone, null, null);
        }

        if (trimmed.StartsWith("zreset ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("reset ", StringComparison.OrdinalIgnoreCase))
        {
            var idText = trimmed[(trimmed.IndexOf(' ') + 1)..].Trim();
            return new CommandRequest(CommandKind.ResetZone, idText, null);
        }

        if (trimmed.StartsWith("say ", StringComparison.OrdinalIgnoreCase))
        {
            var message = trimmed[4..].Trim();
            return new CommandRequest(CommandKind.Say, message, null);
        }

        if (trimmed.Equals("say", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.Say, string.Empty, null);
        }

        if (trimmed.Equals("import-legacy", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandRequest(CommandKind.ImportLegacy, null, null);
        }

        if (trimmed.StartsWith("import-legacy ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = trimmed[14..].Trim();
            return new CommandRequest(CommandKind.ImportLegacy, argument, null);
        }

        if (TryParseDirection(trimmed, out var direction))
        {
            return new CommandRequest(CommandKind.Move, null, direction);
        }

        if (trimmed.StartsWith("go ", StringComparison.OrdinalIgnoreCase)
            && TryParseDirection(trimmed[3..], out direction))
        {
            return new CommandRequest(CommandKind.Move, null, direction);
        }

        return new CommandRequest(CommandKind.Unknown, null, null);
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

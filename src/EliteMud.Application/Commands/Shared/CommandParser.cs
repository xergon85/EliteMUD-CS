using EliteMud.Game;

namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Represents a parsed command from user input.
/// Commands are routed by Verb to handlers decorated with [Command] attributes.
/// </summary>
public sealed record CommandRequest(
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
            return new CommandRequest("", null, null);
        }

        // Extract verb (first word) and argument (remainder)
        var spaceIndex = trimmed.IndexOf(' ');
        var verb = spaceIndex == -1 
            ? trimmed.ToLowerInvariant()
            : trimmed[..spaceIndex].ToLowerInvariant();
        
        var argument = spaceIndex == -1 
            ? null 
            : trimmed[(spaceIndex + 1)..].Trim();

        // Special case: Check if verb is a direction - route to "move" command
        if (TryParseDirection(verb, out var direction))
        {
            return new CommandRequest("move", null, direction);
        }

        // Special case: "go <direction>" - extract direction and route to "move"
        if (verb == "go" && argument != null && TryParseDirection(argument, out direction))
        {
            return new CommandRequest("move", null, direction);
        }

        return new CommandRequest(verb, argument, null);
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

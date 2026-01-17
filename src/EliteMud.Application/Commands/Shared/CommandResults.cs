namespace EliteMud.Application.Commands.Shared;

public sealed record MoveResult(bool Moved, string? Message)
{
    public static MoveResult Success() => new(true, null);

    public static MoveResult Failed(string message) => new(false, message);
}

public sealed record ResetZoneResult(bool Success, string Message)
{
    public static ResetZoneResult Failed(string message) => new(false, message);

    public static ResetZoneResult Succeeded(string message) => new(true, message);
}

public sealed record SayResult(bool Success, string Message, string? BroadcastMessage)
{
    public static SayResult Failed(string message) => new(false, message, null);

    public static SayResult Succeeded(string message, string broadcastMessage) => new(true, message, broadcastMessage);
}

public sealed record RoomView(
    string Name,
    string Description,
    IReadOnlyList<string> MobLines,
    string ExitLine);

public sealed record WhoResult(IReadOnlyList<string> Names);

namespace EliteMud.Application.Commands.Shared;


public sealed record CommandResult(bool Success, string Message)
{
    public static CommandResult Ok(string message) => new(true, message);
    public static CommandResult Fail(string message) => new(false, message);
}

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
    IReadOnlyList<string> ObjectLines,
    IReadOnlyList<string> PlayerLines,
    string ExitLine);

public sealed record WhoResult(IReadOnlyList<string> Names);

public sealed record TellResult(bool Success, string Message, string? RecipientMessage, int? RecipientConnectionId, bool IsHistoryRequest)
{
    public static TellResult Failed(string message) => new(false, message, null, null, false);

    public static TellResult Succeeded(string message, string recipientMessage, int recipientConnectionId) =>
        new(true, message, recipientMessage, recipientConnectionId, false);
    
    public static TellResult ShowHistory() => new(false, string.Empty, null, null, true);
}

public sealed record ReplyResult(bool Success, string Message, string? RecipientMessage, int? RecipientConnectionId)
{
    public static ReplyResult Failed(string message) => new(false, message, null, null);

    public static ReplyResult Succeeded(string message, string recipientMessage, int recipientConnectionId) =>
        new(true, message, recipientMessage, recipientConnectionId);
}

public sealed record EmoteResult(bool Success, string Message, string? BroadcastMessage)
{
    public static EmoteResult Failed(string message) => new(false, message, null);

    public static EmoteResult Succeeded(string message, string broadcastMessage) => new(true, message, broadcastMessage);
}

public sealed record GossipResult(bool Success, string Message, string? BroadcastMessage, bool IsHistoryRequest)
{
    public static GossipResult Failed(string message) => new(false, message, null, false);

    public static GossipResult Succeeded(string message, string broadcastMessage) => new(true, message, broadcastMessage, false);
    
    public static GossipResult ShowHistory() => new(false, string.Empty, null, true);
}

public sealed record ShoutResult(bool Success, string Message, string? BroadcastMessage, bool IsHistoryRequest)
{
    public static ShoutResult Failed(string message) => new(false, message, null, false);

    public static ShoutResult Succeeded(string message, string broadcastMessage) => new(true, message, broadcastMessage, false);
    
    public static ShoutResult ShowHistory() => new(false, string.Empty, null, true);
}

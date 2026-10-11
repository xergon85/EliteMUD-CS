namespace EliteMud.Application.World.Validation;

public enum ContentSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// One problem found in the world content.
/// </summary>
/// <param name="Category">Short stable name, e.g. "Exit to missing room"; used to group the report.</param>
/// <param name="Location">Where it is, e.g. "zone 30 Northern Midgaard, room 3001".</param>
/// <param name="AcceptedReason">Set when the finding is known and accepted (content/meta/accepted-findings.json).</param>
public sealed record ContentFinding(
    ContentSeverity Severity,
    string Category,
    string Location,
    string Message,
    string? AcceptedReason = null)
{
    public bool IsAccepted => AcceptedReason is not null;
}

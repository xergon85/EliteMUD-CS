namespace EliteMud.Application.World.Validation;

/// <summary>
/// A known content finding that has been looked at and accepted, with the reason.
/// Matches by category, plus text contained in the location and message (empty matches anything).
/// </summary>
public sealed record AcceptedFinding(string Category, string Location, string Message, string Reason)
{
    public bool Matches(ContentFinding finding) =>
        string.Equals(finding.Category, Category, StringComparison.Ordinal)
        && finding.Location.Contains(Location, StringComparison.Ordinal)
        && finding.Message.Contains(Message, StringComparison.Ordinal);
}

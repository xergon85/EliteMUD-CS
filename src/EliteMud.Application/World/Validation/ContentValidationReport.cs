namespace EliteMud.Application.World.Validation;

/// <summary>
/// Text output for content validation findings.
/// </summary>
public static class ContentValidationReport
{
    /// <summary>One line per category with its count, errors first.</summary>
    public static IEnumerable<string> Summarize(IReadOnlyList<ContentFinding> findings) =>
        findings
            .GroupBy(finding => (finding.Severity, finding.Category))
            .OrderBy(group => group.Key.Severity)
            .ThenBy(group => group.Key.Category)
            .Select(group => $"{group.Key.Severity,-7} {group.Count(),5}  {group.Key.Category}");

    /// <summary>Every finding, grouped by category, as tab-separated lines.</summary>
    public static IEnumerable<string> Details(IReadOnlyList<ContentFinding> findings) =>
        findings
            .OrderBy(finding => finding.Severity)
            .ThenBy(finding => finding.Category)
            .ThenBy(finding => finding.Location, StringComparer.Ordinal)
            .Select(finding => $"{finding.Severity}\t{finding.Category}\t{finding.Location}\t{finding.Message}");
}

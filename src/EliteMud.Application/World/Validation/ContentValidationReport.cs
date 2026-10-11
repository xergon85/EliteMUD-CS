namespace EliteMud.Application.World.Validation;

/// <summary>
/// Text output for content validation findings.
/// </summary>
public static class ContentValidationReport
{
    /// <summary>One line per category with its count, errors first.</summary>
    public static IEnumerable<string> Summarize(IReadOnlyList<ContentFinding> findings) =>
        findings
            .GroupBy(finding => (Label: Label(finding), Order: Order(finding), finding.Category))
            .OrderBy(group => group.Key.Order)
            .ThenBy(group => group.Key.Category)
            .Select(group => $"{group.Key.Label,-8} {group.Count(),5}  {group.Key.Category}");

    public static int CountErrors(IReadOnlyList<ContentFinding> findings) =>
        findings.Count(finding => finding.Severity == ContentSeverity.Error && !finding.IsAccepted);

    // Accepted findings are listed after everything still open
    private static string Label(ContentFinding finding) => finding.IsAccepted ? "Accepted" : finding.Severity.ToString();

    private static int Order(ContentFinding finding) => finding.IsAccepted ? 10 : (int)finding.Severity;

    /// <summary>Every finding, grouped by category, as tab-separated lines.</summary>
    public static IEnumerable<string> Details(IReadOnlyList<ContentFinding> findings) =>
        findings
            .OrderBy(Order)
            .ThenBy(finding => finding.Category)
            .ThenBy(finding => finding.Location, StringComparer.Ordinal)
            .Select(finding => finding.IsAccepted
                ? $"Accepted\t{finding.Category}\t{finding.Location}\t{finding.Message}\t(was {finding.Severity}: {finding.AcceptedReason})"
                : $"{finding.Severity}\t{finding.Category}\t{finding.Location}\t{finding.Message}");
}

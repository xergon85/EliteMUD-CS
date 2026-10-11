namespace EliteMud.Game;

/// <summary>
/// XP needed for each level (content/progression/xp-curves/&lt;id&gt;.json), shared by several classes.
/// </summary>
/// <param name="Thresholds">XP needed to gain each level; index 0 is level 1.</param>
/// <param name="LegacyRows">The legacy titles[][] XP column the thresholds were generated from.</param>
public sealed record XpCurveDefinition(string Id, IReadOnlyList<int> Thresholds, IReadOnlyList<int> LegacyRows)
{
    /// <summary>XP needed to gain a level at <paramref name="level"/> (clamped to the curve's range).</summary>
    public int XpNeeded(int level) => Thresholds[Math.Clamp(level, 1, Thresholds.Count) - 1];
}

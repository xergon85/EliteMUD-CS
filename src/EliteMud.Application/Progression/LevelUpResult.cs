namespace EliteMud.Application.Progression;

/// <summary>What one level-up gives: added maximum points and the stats after the improvement roll.</summary>
/// <param name="Messages">Lines for the player, one per improved stat.</param>
public sealed record LevelUpResult(
    int HitPointGain,
    int ManaGain,
    int MovementGain,
    AbilityScores Abilities,
    IReadOnlyList<string> Messages);

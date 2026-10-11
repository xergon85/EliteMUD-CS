namespace EliteMud.Application.Progression;

/// <summary>The outcome of one experience gain or loss.</summary>
/// <param name="Experience">XP after the gain; 0 after a level-up, as in legacy.</param>
/// <param name="Change">XP actually added (after the cap) or removed (negative).</param>
/// <param name="LeveledUp">True when the gain crossed the threshold; the caller then advances the level.</param>
/// <param name="Messages">Lines for the player, in order.</param>
public sealed record ExperienceGain(int Experience, int Change, bool LeveledUp, IReadOnlyList<string> Messages);

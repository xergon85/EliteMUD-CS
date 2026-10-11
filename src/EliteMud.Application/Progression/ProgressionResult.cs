namespace EliteMud.Application.Progression;

/// <summary>What an experience change did to a player.</summary>
/// <param name="Messages">Lines to send to the player, in order.</param>
/// <param name="LeveledUp">True when the player gained a level; the caller should save the character.</param>
public sealed record ProgressionResult(IReadOnlyList<string> Messages, bool LeveledUp);

namespace EliteMud.Application.Progression;

/// <summary>What setlevel did: the level change and the maximum points gained on the way up.</summary>
/// <param name="Rebuilt">True when the character was reset to a new level 1 first (same or lower level).</param>
public sealed record SetLevelResult(
    int FromLevel, int ToLevel, bool Rebuilt, int HitPointGain, int ManaGain, int MovementGain);

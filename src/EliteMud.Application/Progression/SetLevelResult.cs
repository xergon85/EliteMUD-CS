namespace EliteMud.Application.Progression;

/// <summary>What setlevel did: the level change and the maximum points gained on the way up.</summary>
public sealed record SetLevelResult(int FromLevel, int ToLevel, int HitPointGain, int ManaGain, int MovementGain);

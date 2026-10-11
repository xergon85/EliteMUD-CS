namespace EliteMud.Application.Progression;

/// <summary>A character's six stats plus the 18/xx strength bonus (legacy str_add, 0-100).</summary>
public sealed record AbilityScores(
    int Strength,
    int StrengthAdd,
    int Intelligence,
    int Wisdom,
    int Dexterity,
    int Constitution,
    int Charisma);

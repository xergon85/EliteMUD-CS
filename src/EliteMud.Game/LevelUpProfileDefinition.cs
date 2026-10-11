namespace EliteMud.Game;

/// <summary>
/// Hit point, mana and movement gains on level-up (content/progression/level-up/&lt;id&gt;.json), shared by several
/// classes. The gains are Lua formulas; see LevelUpInputs for their variables.
/// </summary>
/// <param name="Source">Where the rules come from in the legacy code.</param>
public sealed record LevelUpProfileDefinition(
    string Id,
    string HpGainFormula,
    string ManaGainFormula,
    string MoveGainFormula,
    string Source);

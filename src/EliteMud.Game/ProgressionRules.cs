namespace EliteMud.Game;

/// <summary>
/// Experience rules shared by all classes (content/progression/rules.json).
/// </summary>
/// <param name="MaxGainFormula">The most XP one gain can give, from the XP needed for the next level and the level.</param>
/// <param name="MobXpCurve">The curve that caps what mobs gain from kills; mobs have no class.</param>
/// <param name="Source">Where in legacy the rules come from.</param>
public sealed record ProgressionRules(string MaxGainFormula, XpCurveDefinition MobXpCurve, string Source);

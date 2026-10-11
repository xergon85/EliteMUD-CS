namespace EliteMud.Application.Formulas;

// Inputs shared by kinds of formulas. Each record's properties are the formula's variables, in camelCase
// (Level -> level). A formula reading anything else is rejected when content loads (FormulaChecker).
// A slot with its own unique inputs gets a nested record in its class instead (e.g. KickSkill.HitInputs).

/// <summary>For formulas that only depend on the actor's level (damage, healing, durations, bonuses).</summary>
public sealed record LevelInputs(int Level);

/// <summary>For skill success rolls, e.g. "return random(1,101) &lt;= skillPercent".</summary>
public sealed record SkillCheckInputs(int SkillPercent);

/// <summary>For spell success rolls.</summary>
public sealed record SpellCheckInputs(int Level, int SpellPercent);

/// <summary>For passive defenses (dodge, parry) deciding whether they trigger.</summary>
public sealed record DefenseCheckInputs(int Damage, int SkillPercent);

/// <summary>For passive defenses (dodge, parry) changing the damage taken.</summary>
public sealed record DefenseEffectInputs(int Damage, int Level);

/// <summary>For formulas that take no variables, e.g. "return 10".</summary>
public sealed record NoInputs;

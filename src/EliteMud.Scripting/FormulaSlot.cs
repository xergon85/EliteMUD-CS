namespace EliteMud.Scripting;

/// <summary>
/// One formula in the content and its contract: the input record whose properties are the formula's variables,
/// and what it must return.
/// </summary>
/// <param name="Name">e.g. "kick.hitFormula"; used in error messages.</param>
/// <param name="Formula">The Lua text from content, or null when the content has none (code then uses its fallback).</param>
/// <param name="InputsType">The input record; its properties become camelCase variables.</param>
/// <param name="Result">Whether the formula returns a number or true/false.</param>
public sealed record FormulaSlot(string Name, string? Formula, Type InputsType, FormulaResult Result);

public enum FormulaResult
{
    Number,
    Boolean
}

/// <summary>
/// Something whose behavior comes from content formulas (a skill, a spell), listing them for the load-time check.
/// </summary>
public interface IFormulaSource
{
    IEnumerable<FormulaSlot> FormulaSlots { get; }
}

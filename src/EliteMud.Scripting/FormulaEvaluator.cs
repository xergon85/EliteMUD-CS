using MoonSharp.Interpreter;

namespace EliteMud.Scripting;

/// <summary>
/// Evaluates Lua formulas for skill mechanics (damage, hit chance, etc.).
/// 
/// Thread Safety: Uses simple lock to ensure sequential evaluation.
/// This is safe for current sequential combat processing in GameTickService.
/// 
/// Formula Examples:
/// - Damage: "return math.max(1, level / 2)"
/// - Hit: "return ((10 - div(victimAC, 10)) * 2) + random(1, 101) &lt;= skillPercent"
/// - Dodge: "return (random(1, 250) + damage) &lt; skillPercent"
///
/// Use div(a, b) where legacy C divides integers; "/" is real division in Lua.
/// Each evaluation sees only its context's variables and the built-ins (math, random, div).
/// </summary>
public sealed class FormulaEvaluator
{
    private readonly Script _script;
    private readonly object _lock = new();

    public FormulaEvaluator()
    {
        _script = new Script();
        RegisterHelpers();
    }

    /// <summary>
    /// Evaluate a formula that returns an integer.
    /// </summary>
    /// <param name="formula">Lua code that returns a number (e.g., "return div(level, 2)")</param>
    /// <param name="context">Object whose properties are the formula's variables (e.g., new { level = 10 })</param>
    /// <param name="name">Name used in error messages, e.g. "kick.damageFormula"</param>
    /// <returns>Integer result (truncated toward zero)</returns>
    /// <exception cref="FormulaEvaluationException">If the formula fails, reads an unknown variable or returns the wrong type</exception>
    public int EvaluateInt(string formula, object context, string? name = null)
    {
        var result = Evaluate(formula, context, name);
        if (result.Type != DataType.Number)
        {
            throw new FormulaEvaluationException(
                $"{Describe(formula, name)} returned {result.Type} but expected a number.");
        }

        return (int)result.Number;
    }

    /// <summary>
    /// Evaluate a formula that returns a boolean.
    /// </summary>
    /// <param name="formula">Lua code that returns true/false (e.g., "return level > 10")</param>
    /// <param name="context">Object whose properties are the formula's variables</param>
    /// <param name="name">Name used in error messages, e.g. "kick.hitFormula"</param>
    /// <returns>Boolean result</returns>
    /// <exception cref="FormulaEvaluationException">If the formula fails, reads an unknown variable or returns the wrong type</exception>
    public bool EvaluateBool(string formula, object context, string? name = null)
    {
        var result = Evaluate(formula, context, name);
        if (result.Type != DataType.Boolean)
        {
            throw new FormulaEvaluationException(
                $"{Describe(formula, name)} returned {result.Type} but expected true or false.");
        }

        return result.Boolean;
    }

    /// <summary>
    /// Validate that a formula is syntactically correct and can be compiled.
    /// Call this at startup to fail fast if formulas are invalid.
    /// </summary>
    /// <param name="formula">Lua code to validate</param>
    /// <exception cref="FormulaEvaluationException">If formula is invalid</exception>
    public void ValidateFormula(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            throw new FormulaEvaluationException("Formula cannot be null or empty");
        }

        lock (_lock)
        {
            try
            {
                // Try to load (compile) the formula
                // This catches syntax errors without executing the code
                _script.LoadString(formula);
            }
            catch (SyntaxErrorException ex)
            {
                throw new FormulaEvaluationException(
                    $"Invalid formula syntax: {formula}\nError: {ex.DecoratedMessage}", ex);
            }
        }
    }

    /// <summary>
    /// Run a formula in its own environment: the context's properties plus the built-ins (math, random, div).
    /// Reading any other name is an error, so a typo or a variable from another formula can't silently
    /// pick up a leftover value.
    /// </summary>
    private DynValue Evaluate(string formula, object context, string? name)
    {
        lock (_lock)
        {
            try
            {
                return _script.DoString(formula, CreateEnvironment(context, formula, name));
            }
            catch (ScriptRuntimeException ex)
            {
                throw new FormulaEvaluationException(
                    $"Lua runtime error in {Describe(formula, name)}\nError: {ex.DecoratedMessage}", ex);
            }
            catch (SyntaxErrorException ex)
            {
                throw new FormulaEvaluationException(
                    $"Lua syntax error in {Describe(formula, name)}\nError: {ex.DecoratedMessage}", ex);
            }
        }
    }

    private Table CreateEnvironment(object context, string formula, string? name)
    {
        var environment = new Table(_script);
        foreach (var property in context.GetType().GetProperties())
        {
            environment[ToVariableName(property.Name)] = property.GetValue(context);
        }

        // Unknown names fall back to the built-ins in the script's globals, and fail if they aren't there either
        var metatable = new Table(_script);
        metatable["__index"] = (Func<Table, DynValue, DynValue>)((_, key) =>
        {
            var builtin = _script.Globals.Get(key);
            if (builtin.IsNil())
            {
                throw new ScriptRuntimeException($"unknown variable '{key.ToPrintString()}'");
            }

            return builtin;
        });
        environment.MetaTable = metatable;
        return environment;
    }

    /// <summary>
    /// Formula variables are camelCase: an input record's VictimAC is "victimAC" in the formula.
    /// </summary>
    public static string ToVariableName(string propertyName) =>
        propertyName.Length == 0 ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    private static string Describe(string formula, string? name) =>
        name is null ? $"formula \"{formula}\"" : $"formula {name} (\"{formula}\")";

    /// <summary>
    /// Register helper functions available to all formulas.
    /// </summary>
    private void RegisterHelpers()
    {
        // Register random(min, max) - inclusive range
        // Legacy EliteMUD: number(from, to) returns from..to inclusive
        _script.Globals["random"] = (Func<int, int, int>)((min, max) =>
        {
            return Random.Shared.Next(min, max + 1); // +1 because Random.Next is exclusive on upper bound
        });

        // div(a, b): integer division like C, truncating toward zero (-55 / 10 = -5).
        // Lua's "/" is real division (-5.5), so formulas ported from legacy C use div where C divides.
        _script.Globals["div"] = (Func<double, double, double>)((a, b) =>
        {
            if (b == 0)
            {
                throw new ScriptRuntimeException("div: division by zero");
            }

            return Math.Truncate(a / b);
        });

        // Lua already has math.min, math.max, math.floor, math.ceil, math.abs, etc.
        // No need to register those - they're built into MoonSharp
    }
}

/// <summary>
/// Exception thrown when formula evaluation fails.
/// </summary>
public sealed class FormulaEvaluationException : Exception
{
    public FormulaEvaluationException(string message) : base(message)
    {
    }

    public FormulaEvaluationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

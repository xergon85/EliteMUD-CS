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
/// - Hit: "return ((10 - victimAC/10) * 2) + random(1, 101) &lt;= skillPercent"
/// - Dodge: "return (random(1, 250) + damage) &lt; skillPercent"
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
    /// <param name="formula">Lua code that returns a number (e.g., "return level / 2")</param>
    /// <param name="context">Anonymous object with variables (e.g., new { level = 10 })</param>
    /// <returns>Integer result</returns>
    /// <exception cref="FormulaEvaluationException">If formula fails to execute or returns wrong type</exception>
    public int EvaluateInt(string formula, object context)
    {
        lock (_lock)
        {
            try
            {
                SetContext(context);
                var result = _script.DoString(formula);

                if (result.Type != DataType.Number)
                {
                    throw new FormulaEvaluationException(
                        $"Formula returned {result.Type} but expected Number. Formula: {formula}");
                }

                return (int)result.Number;
            }
            catch (ScriptRuntimeException ex)
            {
                throw new FormulaEvaluationException(
                    $"Lua runtime error in formula: {formula}\nError: {ex.DecoratedMessage}", ex);
            }
            catch (SyntaxErrorException ex)
            {
                throw new FormulaEvaluationException(
                    $"Lua syntax error in formula: {formula}\nError: {ex.DecoratedMessage}", ex);
            }
        }
    }

    /// <summary>
    /// Evaluate a formula that returns a boolean.
    /// </summary>
    /// <param name="formula">Lua code that returns true/false (e.g., "return level > 10")</param>
    /// <param name="context">Anonymous object with variables (e.g., new { level = 10 })</param>
    /// <returns>Boolean result</returns>
    /// <exception cref="FormulaEvaluationException">If formula fails to execute or returns wrong type</exception>
    public bool EvaluateBool(string formula, object context)
    {
        lock (_lock)
        {
            try
            {
                SetContext(context);
                var result = _script.DoString(formula);

                if (result.Type != DataType.Boolean)
                {
                    throw new FormulaEvaluationException(
                        $"Formula returned {result.Type} but expected Boolean. Formula: {formula}");
                }

                return result.Boolean;
            }
            catch (ScriptRuntimeException ex)
            {
                throw new FormulaEvaluationException(
                    $"Lua runtime error in formula: {formula}\nError: {ex.DecoratedMessage}", ex);
            }
            catch (SyntaxErrorException ex)
            {
                throw new FormulaEvaluationException(
                    $"Lua syntax error in formula: {formula}\nError: {ex.DecoratedMessage}", ex);
            }
        }
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
    /// Set context variables in Lua globals from anonymous object properties.
    /// Example: new { level = 10, skillPercent = 75 } sets globals: level=10, skillPercent=75
    /// 
    /// Note: We don't clear globals because that removes built-in Lua libraries (math, string, etc).
    /// Variables are overwritten each time, so no leakage occurs.
    /// </summary>
    private void SetContext(object context)
    {
        if (context == null)
        {
            return;
        }

        // Use reflection to set Lua globals from anonymous object properties
        var properties = context.GetType().GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(context);
            _script.Globals[prop.Name] = value;
        }
    }

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

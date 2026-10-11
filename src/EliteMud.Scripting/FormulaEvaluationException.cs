namespace EliteMud.Scripting;

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

namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Result of command validation.
/// Contains success status and optional error message.
/// </summary>
public sealed class ValidationResult
{
    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    /// <summary>
    /// Create a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, null);

    /// <summary>
    /// Create a failed validation result with an error message.
    /// </summary>
    public static ValidationResult Fail(string errorMessage) => new(false, errorMessage);
}

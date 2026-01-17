namespace EliteMud.Application;

public sealed class LoginResult
{
    private LoginResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }

    public string Message { get; }

    public static LoginResult Accepted() => new(true, string.Empty);

    public static LoginResult Rejected(string message) => new(false, message);
}

public sealed class LoginHandler
{
    private readonly PlayerNameValidator _validator = new();

    public LoginResult ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return LoginResult.Rejected("Names must be 3-16 letters.");
        }

        return _validator.IsValid(trimmed)
            ? LoginResult.Accepted()
            : LoginResult.Rejected("Names must be 3-16 letters.");
    }
}

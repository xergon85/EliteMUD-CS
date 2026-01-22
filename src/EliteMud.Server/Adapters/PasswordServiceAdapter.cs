using EliteMud.Application.Session.Authentication;
using EliteMud.Data.Services;

namespace EliteMud.Server.Adapters;

/// <summary>
/// Adapter that bridges BCryptPasswordService (Infrastructure) to IPasswordService (Application).
/// Required to avoid circular dependency between Application and Data layers.
/// </summary>
internal sealed class PasswordServiceAdapter : IPasswordService
{
    private readonly BCryptPasswordService _bcryptService = new();

    public string HashPassword(string password)
    {
        return _bcryptService.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return _bcryptService.VerifyPassword(password, passwordHash);
    }
}

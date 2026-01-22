namespace EliteMud.Data.Services;

/// <summary>
/// BCrypt-based implementation of password hashing.
/// Infrastructure layer - depends on external BCrypt library.
/// Implements EliteMud.Application.Session.Authentication.IPasswordService
/// but does not reference Application to avoid circular dependency.
/// </summary>
public class BCryptPasswordService
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}

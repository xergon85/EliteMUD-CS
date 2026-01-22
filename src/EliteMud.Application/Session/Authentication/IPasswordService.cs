namespace EliteMud.Application.Session.Authentication;

/// <summary>
/// Service for hashing and verifying passwords.
/// </summary>
public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

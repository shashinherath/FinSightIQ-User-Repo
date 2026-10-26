namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Password hashing and verification abstraction using BCrypt.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

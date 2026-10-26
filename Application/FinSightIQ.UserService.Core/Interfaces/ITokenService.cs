using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Cryptographic token issuance for RS256 JWT access tokens and hashed refresh tokens.
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(UserRecordDto user);
    string GenerateAccessToken(UserDto user);
    (string RawToken, string TokenHash) GenerateRefreshToken();
    string HashRefreshToken(string rawToken);
    int AccessTokenLifetimeSeconds { get; }
    TimeSpan RefreshTokenLifetime { get; }
}

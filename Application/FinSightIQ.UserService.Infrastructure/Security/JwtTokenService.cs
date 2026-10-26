using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinSightIQ.UserService.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private readonly IRsaKeyProvider _keyProvider;
    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtTokenService(
        IRsaKeyProvider keyProvider,
        IOptions<JwtOptions> options)
    {
        _keyProvider = keyProvider;
        _options = options.Value;
    }

    public int AccessTokenLifetimeSeconds => _options.AccessTokenLifetimeMinutes * 60;
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenLifetimeDays);

    public string GenerateAccessToken(UserRecordDto user)
    {
        return GenerateToken(user.Id, user.Email, user.Role.ToString());
    }

    public string GenerateAccessToken(UserDto user)
    {
        return GenerateToken(user.Id, user.Email, user.Role);
    }

    private string GenerateToken(Guid userId, string email, string role)
    {
        DateTime now = DateTime.UtcNow;
        DateTime expires = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        List<Claim> claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("email", email),
            new("role", role),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        SigningCredentials credentials = new SigningCredentials(_keyProvider.SigningKey, SecurityAlgorithms.RsaSha256);

        SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = credentials
        };

        SecurityToken token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public (string RawToken, string TokenHash) GenerateRefreshToken()
    {
        byte[] randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        string rawToken = Base64UrlEncoder.Encode(randomBytes);
        string tokenHash = HashRefreshToken(rawToken);
        return (rawToken, tokenHash);
    }

    public string HashRefreshToken(string rawToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawToken);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

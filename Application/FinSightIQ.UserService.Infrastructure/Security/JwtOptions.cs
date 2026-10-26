namespace FinSightIQ.UserService.Infrastructure.Security;

/// <summary>
/// Strongly-typed options for RS256 JWT access tokens and refresh tokens.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "finsightiq";
    public string Audience { get; set; } = "finsightiq";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 7;
    public string? PrivateKeyPem { get; set; }
    public string? PublicKeyPem { get; set; }
    public string KeyId { get; set; } = "finsightiq-key";
}

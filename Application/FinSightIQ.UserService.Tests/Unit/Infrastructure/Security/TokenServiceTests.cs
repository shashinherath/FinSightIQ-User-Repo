using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace FinSightIQ.UserService.Tests.Unit.Infrastructure.Security;

public sealed class TokenServiceTests
{
    private readonly JwtTokenService _tokenService;
    private readonly RsaKeyProvider _keyProvider;
    private readonly JwtOptions _options;

    public TokenServiceTests()
    {
        _options = new JwtOptions
        {
            Issuer = "finsightiq",
            Audience = "finsightiq",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7,
            KeyId = "finsightiq-test-key"
        };

        IOptions<JwtOptions> optionsWrapper = Options.Create(_options);
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        env.ContentRootPath.Returns(Directory.GetCurrentDirectory());

        _keyProvider = new RsaKeyProvider(optionsWrapper, env, NullLogger<RsaKeyProvider>.Instance);
        _tokenService = new JwtTokenService(_keyProvider, optionsWrapper);
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeStandardAndCustomClaims()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        UserRecordDto user = new UserRecordDto(
            userId,
            "analyst@example.com",
            "$2a$12$mockhash",
            UserRole.Analyst,
            isSuspended: false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        // Act
        string rawToken = _tokenService.GenerateAccessToken(user);

        // Assert
        rawToken.Should().NotBeNullOrWhiteSpace();

        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt = handler.ReadJwtToken(rawToken);

        jwt.Issuer.Should().Be("finsightiq");
        jwt.Audiences.Should().Contain("finsightiq");
        jwt.Header.Kid.Should().Be("finsightiq-test-key");

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "analyst@example.com");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Analyst");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateAccessToken_ShouldBeVerifiableWithPublicKey()
    {
        // Arrange
        UserRecordDto user = new UserRecordDto(
            Guid.NewGuid(),
            "admin@example.com",
            "$2a$12$mockhash",
            UserRole.Administrator,
            isSuspended: false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        string token = _tokenService.GenerateAccessToken(user);

        TokenValidationParameters validationParams = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = _keyProvider.ValidationKey,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

        // Act & Assert
        ClaimsPrincipal principal = handler.ValidateToken(token, validationParams, out SecurityToken validatedToken);
        principal.Should().NotBeNull();
        validatedToken.Should().NotBeNull();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnCryptographicEntropy()
    {
        // Act
        (string raw1, string hash1) = _tokenService.GenerateRefreshToken();
        (string raw2, string hash2) = _tokenService.GenerateRefreshToken();

        // Assert
        raw1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().NotBeNullOrWhiteSpace();
        raw1.Should().NotBe(raw2);
        hash1.Should().NotBe(hash2);
        hash1.Length.Should().Be(64); // SHA-256 hex string = 64 characters
    }

    [Fact]
    public void HashRefreshToken_ShouldProduceDeterministicSha256()
    {
        // Arrange
        const string raw = "test-refresh-token-value-123456";

        // Act
        string hash1 = _tokenService.HashRefreshToken(raw);
        string hash2 = _tokenService.HashRefreshToken(raw);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Length.Should().Be(64);
    }
}

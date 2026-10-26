using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FinSightIQ.UserService.Core.Services;
using FinSightIQ.UserService.Core.Validators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinSightIQ.UserService.Tests.Unit.Core.Services;

public sealed class UserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();

    private readonly UserService.Core.Services.UserService _sut;

    public UserServiceTests()
    {
        _tokenService.AccessTokenLifetimeSeconds.Returns(900);
        _tokenService.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _tokenService.HashRefreshToken(Arg.Any<string>()).Returns(ci => "hash_" + ci.Arg<string>());
        _tokenService.GenerateRefreshToken().Returns(("new_raw_token", "hash_new_raw_token"));
        _tokenService.GenerateAccessToken(Arg.Any<UserRecordDto>()).Returns("mock.jwt.token");

        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("$2a$12$mockbcryptmockbcryptmockbcryptmockbcryptmockbcrypt60chars");
        _passwordHasher.VerifyPassword("Str0ngPassphrase!", "$2a$12$correcthash").Returns(true);

        _sut = new UserService.Core.Services.UserService(
            _userRepository,
            _refreshTokenRepository,
            _passwordHasher,
            _tokenService,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new ChangePasswordRequestValidator(),
            NullLogger<UserService.Core.Services.UserService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_ShouldReturnSuccessWithAnalystRole()
    {
        // Arrange (TC-AUTH-01)
        RegisterRequest request = new RegisterRequest("analyst@example.com", "Str0ngPassphrase!");
        _userRepository.ExistsByEmailAsync(Arg.Any<string>()).Returns(false);
        _userRepository.CreateAsync(Arg.Any<UserRecordDto>())
            .Returns(ci => ci.Arg<UserRecordDto>());

        // Act
        Result<RegisterResponse> result = await _sut.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("analyst@example.com");
        result.Value.Role.Should().Be("Analyst");
        result.Value.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ShouldReturnEmailAlreadyRegistered()
    {
        // Arrange (TC-AUTH-02)
        RegisterRequest request = new RegisterRequest("duplicate@example.com", "Str0ngPassphrase!");
        _userRepository.ExistsByEmailAsync("duplicate@example.com").Returns(true);

        // Act
        Result<RegisterResponse> result = await _sut.RegisterAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task RegisterAsync_WithDifferentCasedExistingEmail_ShouldRejectAsDuplicate()
    {
        // Arrange (TC-AUTH-03)
        RegisterRequest request = new RegisterRequest("Alice@Example.COM", "Str0ngPassphrase!");
        _userRepository.ExistsByEmailAsync("alice@example.com").Returns(true);

        // Act
        Result<RegisterResponse> result = await _sut.RegisterAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnAccessAndRefreshTokens()
    {
        // Arrange (TC-AUTH-06)
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), "analyst@example.com", "$2a$12$correcthash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _userRepository.GetByEmailAsync("analyst@example.com").Returns(user);

        LoginRequest request = new LoginRequest("analyst@example.com", "Str0ngPassphrase!");

        // Act
        Result<(LoginResponse Response, string RawRefreshToken)> result = await _sut.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.AccessToken.Should().Be("mock.jwt.token");
        result.Value.Response.ExpiresIn.Should().Be(900);
        result.Value.Response.User.Role.Should().Be("Analyst");
        result.Value.RawRefreshToken.Should().Be("new_raw_token");
        await _refreshTokenRepository.Received(1).CreateAsync(Arg.Any<RefreshTokenRecordDto>());
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ShouldReturnUnauthenticated()
    {
        // Arrange (TC-AUTH-07)
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), "analyst@example.com", "$2a$12$correcthash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _userRepository.GetByEmailAsync("analyst@example.com").Returns(user);

        LoginRequest request = new LoginRequest("analyst@example.com", "WrongPassword123!");

        // Act
        Result<(LoginResponse Response, string RawRefreshToken)> result = await _sut.LoginAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task LoginAsync_WithSuspendedUser_ShouldReturnAccountSuspended()
    {
        // Arrange (TC-AUTH-15)
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), "suspended@example.com", "$2a$12$correcthash", UserRole.Analyst, isSuspended: true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _userRepository.GetByEmailAsync("suspended@example.com").Returns(user);

        LoginRequest request = new LoginRequest("suspended@example.com", "Str0ngPassphrase!");

        // Act
        Result<(LoginResponse Response, string RawRefreshToken)> result = await _sut.LoginAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNT_SUSPENDED");
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_ShouldRotateToken()
    {
        // Arrange (TC-AUTH-08)
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), "analyst@example.com", "hash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        RefreshTokenRecordDto activeToken = new RefreshTokenRecordDto(Guid.NewGuid(), user.Id, "hash_valid_token", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(5));

        _refreshTokenRepository.GetByHashAsync("hash_valid_token").Returns(activeToken);
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        // Act
        Result<(LoginResponse Response, string RawRefreshToken)> result = await _sut.RefreshAsync("valid_token");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RawRefreshToken.Should().Be("new_raw_token");
        await _refreshTokenRepository.Received(1).RotateTokenAsync(activeToken.Id, Arg.Any<RefreshTokenRecordDto>());
    }

    [Fact]
    public async Task RefreshAsync_WithReplayedRotatedToken_ShouldRevokeFamilyAndReturnTokenReuseDetected()
    {
        // Arrange (TC-AUTH-09)
        Guid familyId = Guid.NewGuid();
        RefreshTokenRecordDto replayedToken = new RefreshTokenRecordDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash_replayed_token",
            familyId,
            DateTimeOffset.UtcNow.AddDays(5),
            usedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        _refreshTokenRepository.GetByHashAsync("hash_replayed_token").Returns(replayedToken);

        // Act
        Result<(LoginResponse Response, string RawRefreshToken)> result = await _sut.RefreshAsync("replayed_token");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TOKEN_REUSE_DETECTED");
        await _refreshTokenRepository.Received(1).RevokeFamilyAsync(familyId);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_ShouldUpdateHashAndRevokeAllSessions()
    {
        // Arrange (TC-AUTH-13)
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), "analyst@example.com", "$2a$12$correcthash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        ChangePasswordRequest request = new ChangePasswordRequest("Str0ngPassphrase!", "NewStr0ngPassphrase!");

        // Act
        Result result = await _sut.ChangePasswordAsync(user.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.Received(1).UpdateAsync(Arg.Is<UserRecordDto>(u => u.Id == user.Id && u.PasswordHash == "$2a$12$mockbcryptmockbcryptmockbcryptmockbcryptmockbcrypt60chars"));
        await _refreshTokenRepository.Received(1).RevokeAllForUserAsync(user.Id);
    }
}

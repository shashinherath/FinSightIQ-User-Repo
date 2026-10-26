using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace FinSightIQ.UserService.Core.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _logger = logger;
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            string firstError = validation.Errors[0].ErrorMessage;
            return Result<RegisterResponse>.Failure("VALIDATION_FAILED", firstError);
        }

        string normalizedEmail = request.Email.Trim().ToLowerInvariant();

        bool exists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
        if (exists)
        {
            return Result<RegisterResponse>.Failure(
                "EMAIL_ALREADY_REGISTERED",
                "An account with this email address already exists.");
        }

        string passwordHash = _passwordHasher.HashPassword(request.Password);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UserRecordDto user = new UserRecordDto(
            Guid.NewGuid(),
            normalizedEmail,
            passwordHash,
            UserRole.Analyst,
            isSuspended: false,
            createdAt: now,
            updatedAt: now);

        try
        {
            UserRecordDto created = await _userRepository.CreateAsync(user, cancellationToken);
            _logger.LogInformation("User registered successfully: UserId={UserId}, Role={Role}", created.Id, created.Role);

            return Result<RegisterResponse>.Success(new RegisterResponse(
                created.Id,
                created.Email,
                created.Role.ToString(),
                created.CreatedAt));
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("ux_users_email_lower", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Concurrent duplicate registration caught for email: {Email}", normalizedEmail);
            return Result<RegisterResponse>.Failure(
                "EMAIL_ALREADY_REGISTERED",
                "An account with this email address already exists.");
        }
    }

    public async Task<Result<(LoginResponse Response, string RawRefreshToken)>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<(LoginResponse, string)>.Failure(
                "VALIDATION_FAILED",
                validation.Errors[0].ErrorMessage);
        }

        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        UserRecordDto? user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        // Mitigate user enumeration: identical response whether email exists or password mismatch
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", normalizedEmail);
            return Result<(LoginResponse, string)>.Failure(
                "UNAUTHENTICATED",
                "Invalid email or password.");
        }

        if (user.IsSuspended)
        {
            _logger.LogWarning("Suspended user attempted login: UserId={UserId}", user.Id);
            return Result<(LoginResponse, string)>.Failure(
                "ACCOUNT_SUSPENDED",
                "Your account has been suspended. Please contact support.");
        }

        string accessToken = _tokenService.GenerateAccessToken(user);
        (string rawRefreshToken, string tokenHash) = _tokenService.GenerateRefreshToken();

        Guid familyId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(_tokenService.RefreshTokenLifetime);

        RefreshTokenRecordDto refreshTokenEntity = new RefreshTokenRecordDto(
            Guid.NewGuid(),
            user.Id,
            tokenHash,
            familyId,
            expiresAt);

        await _refreshTokenRepository.CreateAsync(refreshTokenEntity, cancellationToken);

        _logger.LogInformation("User logged in: UserId={UserId}, FamilyId={FamilyId}", user.Id, familyId);

        LoginResponse response = new LoginResponse(
            accessToken,
            "Bearer",
            _tokenService.AccessTokenLifetimeSeconds,
            new UserDto(user.Id, user.Email, user.Role.ToString()));

        return Result<(LoginResponse, string)>.Success((response, rawRefreshToken));
    }

    public async Task<Result<(LoginResponse Response, string RawRefreshToken)>> RefreshAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result<(LoginResponse, string)>.Failure(
                "UNAUTHENTICATED",
                "Refresh token is required.");
        }

        string tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        RefreshTokenRecordDto? token = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);

        if (token == null)
        {
            _logger.LogWarning("Refresh failed: Token hash not found.");
            return Result<(LoginResponse, string)>.Failure(
                "UNAUTHENTICATED",
                "Invalid refresh token.");
        }

        // Token Reuse Detection
        if (token.IsUsed)
        {
            _logger.LogCritical(
                "SECURITY ALERT: Refresh token reuse detected! FamilyId={FamilyId}, UserId={UserId}. Revoking token family.",
                token.FamilyId,
                token.UserId);

            await _refreshTokenRepository.RevokeFamilyAsync(token.FamilyId, cancellationToken);

            return Result<(LoginResponse, string)>.Failure(
                "TOKEN_REUSE_DETECTED",
                "A previously used refresh token was presented. All active sessions have been invalidated for security.");
        }

        if (token.IsRevoked)
        {
            _logger.LogWarning("Refresh failed: Token already revoked. TokenId={TokenId}", token.Id);
            return Result<(LoginResponse, string)>.Failure(
                "UNAUTHENTICATED",
                "Refresh token has been revoked.");
        }

        if (token.IsExpired)
        {
            _logger.LogInformation("Refresh failed: Token expired. TokenId={TokenId}", token.Id);
            return Result<(LoginResponse, string)>.Failure(
                "TOKEN_EXPIRED",
                "Refresh token has expired. Please log in again.");
        }

        UserRecordDto? user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Refresh failed: User not found or inactive. UserId={UserId}", token.UserId);
            return Result<(LoginResponse, string)>.Failure(
                "ACCOUNT_SUSPENDED",
                "User account is not active.");
        }

        // Token Rotation: create new token in the SAME family
        (string newRawRefreshToken, string newTokenHash) = _tokenService.GenerateRefreshToken();
        DateTimeOffset newExpiresAt = DateTimeOffset.UtcNow.Add(_tokenService.RefreshTokenLifetime);

        RefreshTokenRecordDto newRefreshToken = new RefreshTokenRecordDto(
            Guid.NewGuid(),
            user.Id,
            newTokenHash,
            token.FamilyId,
            newExpiresAt);

        await _refreshTokenRepository.RotateTokenAsync(token.Id, newRefreshToken, cancellationToken);

        string accessToken = _tokenService.GenerateAccessToken(user);

        LoginResponse response = new LoginResponse(
            accessToken,
            "Bearer",
            _tokenService.AccessTokenLifetimeSeconds,
            new UserDto(user.Id, user.Email, user.Role.ToString()));

        _logger.LogInformation("Token rotated successfully for UserId={UserId}, FamilyId={FamilyId}", user.Id, token.FamilyId);

        return Result<(LoginResponse, string)>.Success((response, newRawRefreshToken));
    }

    public async Task<Result> LogoutAsync(
        string? rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            string tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
            RefreshTokenRecordDto? token = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);
            if (token != null && !token.IsRevoked)
            {
                await _refreshTokenRepository.RevokeAsync(token.Id, cancellationToken);
                _logger.LogInformation("Refresh token revoked on logout: TokenId={TokenId}", token.Id);
            }
        }

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure("VALIDATION_FAILED", validation.Errors[0].ErrorMessage);
        }

        UserRecordDto? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || user.DeletedAt != null)
        {
            return Result.Failure("RESOURCE_NOT_FOUND", "User not found.");
        }

        if (user.IsSuspended)
        {
            return Result.Failure("ACCOUNT_SUSPENDED", "Account is suspended.");
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure("UNAUTHENTICATED", "Current password is incorrect.");
        }

        string newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        UserRecordDto updatedUser = user with
        {
            PasswordHash = newPasswordHash,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _userRepository.UpdateAsync(updatedUser, cancellationToken);

        // Invalidate all refresh tokens on password change (US-AUTH-07, TC-AUTH-13)
        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
        _logger.LogInformation("Password changed and sessions revoked for UserId={UserId}", userId);

        return Result.Success();
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        UserRecordDto? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || user.DeletedAt != null)
        {
            return Result<UserProfileResponse>.Failure("RESOURCE_NOT_FOUND", "User not found.");
        }

        if (user.IsSuspended)
        {
            return Result<UserProfileResponse>.Failure("ACCOUNT_SUSPENDED", "Account is suspended.");
        }

        return Result<UserProfileResponse>.Success(new UserProfileResponse(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.CreatedAt));
    }

    public async Task<Result<PagedUsersResponse>> GetUsersAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(0, offset);

        int total = await _userRepository.GetCountAsync(cancellationToken);
        IReadOnlyList<UserRecordDto> users = await _userRepository.GetPagedAsync(limit, offset, cancellationToken);

        List<UserAdminDto> data = users.Select(u => new UserAdminDto(
            u.Id,
            u.Email,
            u.Role.ToString(),
            u.IsSuspended,
            u.CreatedAt,
            u.UpdatedAt,
            u.DeletedAt)).ToList();

        bool hasMore = offset + users.Count < total;
        PaginationMetadata pagination = new PaginationMetadata(total, limit, offset, hasMore);

        return Result<PagedUsersResponse>.Success(new PagedUsersResponse(data, pagination));
    }

    public async Task<Result> SuspendUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        UserRecordDto? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || user.DeletedAt != null)
        {
            return Result.Failure("RESOURCE_NOT_FOUND", "User not found.");
        }

        UserRecordDto updatedUser = user with
        {
            IsSuspended = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _userRepository.UpdateAsync(updatedUser, cancellationToken);
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);

        _logger.LogInformation("User suspended: UserId={UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> ReinstateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        UserRecordDto? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || user.DeletedAt != null)
        {
            return Result.Failure("RESOURCE_NOT_FOUND", "User not found.");
        }

        UserRecordDto updatedUser = user with
        {
            IsSuspended = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _userRepository.UpdateAsync(updatedUser, cancellationToken);

        _logger.LogInformation("User reinstated: UserId={UserId}", userId);
        return Result.Success();
    }
}

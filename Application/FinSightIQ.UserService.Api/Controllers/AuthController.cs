using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Api.Extensions;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSightIQ.UserService.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private const string _refreshTokenCookieName = "refreshToken";

    private readonly IUserService _userService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing user registration for email: {Email}", request.Email);

        ValidationResult validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Registration validation failed for email: {Email}", request.Email);

            Dictionary<string, string[]> errors = validation.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return this.ToProblemResult(
                new Error("VALIDATION_FAILED", "One or more validation errors occurred."),
                errors);
        }

        Result<RegisterResponse> result = await _userService.RegisterAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Registration failed for email {Email}: {Code} - {Message}",
                request.Email, result.Error.Code, result.Error.Message);
            return this.ToProblemResult(result.Error);
        }

        _logger.LogInformation("User registration successful: UserId={UserId}", result.Value.UserId);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing login attempt for email: {Email}", request.Email);

        ValidationResult validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Login validation failed for email: {Email}", request.Email);

            Dictionary<string, string[]> errors = validation.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return this.ToProblemResult(
                new Error("VALIDATION_FAILED", "One or more validation errors occurred."),
                errors);
        }

        Result<(LoginResponse Response, string RawRefreshToken)> result = await _userService.LoginAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Login failed for email {Email}: {Code}", request.Email, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        SetRefreshTokenCookie(result.Value.RawRefreshToken);
        _logger.LogInformation("User login successful: UserId={UserId}", result.Value.Response.User.Id);

        return Ok(result.Value.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing refresh token request.");

        string? rawRefreshToken = Request.Cookies[_refreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            _logger.LogWarning("Refresh token request rejected: cookie missing.");
            return this.ToProblemResult(new Error("UNAUTHENTICATED", "Refresh token cookie is missing."));
        }

        Result<(LoginResponse Response, string RawRefreshToken)> result = await _userService.RefreshAsync(rawRefreshToken, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Token refresh failed: {Code} - {Message}", result.Error.Code, result.Error.Message);
            Response.Cookies.Delete(_refreshTokenCookieName);
            return this.ToProblemResult(result.Error);
        }

        SetRefreshTokenCookie(result.Value.RawRefreshToken);
        _logger.LogInformation("Token refreshed successfully: UserId={UserId}", result.Value.Response.User.Id);

        return Ok(result.Value.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing logout request.");

        string? rawRefreshToken = Request.Cookies[_refreshTokenCookieName];
        await _userService.LogoutAsync(rawRefreshToken, cancellationToken);

        Response.Cookies.Delete(_refreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = IsCookieSecureRequired(),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        _logger.LogInformation("User logged out successfully.");
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.GetUserId();
        if (currentUserId == null)
        {
            _logger.LogWarning("Unauthorized attempt to change password.");
            return this.ToProblemResult(new Error("UNAUTHENTICATED", "Authentication is required."));
        }

        _logger.LogInformation("Processing change password request for UserId={UserId}", currentUserId.Value);

        ValidationResult validation = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Change password validation failed for UserId={UserId}", currentUserId.Value);

            Dictionary<string, string[]> errors = validation.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return this.ToProblemResult(
                new Error("VALIDATION_FAILED", "One or more validation errors occurred."),
                errors);
        }

        Result result = await _userService.ChangePasswordAsync(currentUserId.Value, request, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Change password failed for UserId={UserId}: {Code}", currentUserId.Value, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        Response.Cookies.Delete(_refreshTokenCookieName);
        _logger.LogInformation("Password updated successfully for UserId={UserId}", currentUserId.Value);

        return Ok(new { message = "Password updated successfully. All active sessions have been invalidated." });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.GetUserId();
        if (currentUserId == null)
        {
            _logger.LogWarning("Unauthorized attempt to fetch profile.");
            return this.ToProblemResult(new Error("UNAUTHENTICATED", "Authentication is required."));
        }

        _logger.LogInformation("Fetching profile for UserId={UserId}", currentUserId.Value);

        Result<UserProfileResponse> result = await _userService.GetProfileAsync(currentUserId.Value, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Get profile failed for UserId={UserId}: {Code}", currentUserId.Value, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    private void SetRefreshTokenCookie(string rawToken)
    {
        bool isSecure = IsCookieSecureRequired();

        CookieOptions cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };

        Response.Cookies.Append(_refreshTokenCookieName, rawToken, cookieOptions);
    }

    private bool IsCookieSecureRequired()
    {
        string? secureConfig = _configuration["Cookie:Secure"];
        if (bool.TryParse(secureConfig, out bool isSecure))
        {
            return isSecure;
        }

        return Request.IsHttps;
    }
}

namespace FinSightIQ.UserService.Core.DTOs;

/// <summary>Inbound payload for account registration.</summary>
public sealed record RegisterRequest(string Email, string Password);

/// <summary>Outbound 201 response for successful account registration.</summary>
public sealed record RegisterResponse(Guid UserId, string Email, string Role, DateTimeOffset CreatedAt);

/// <summary>Inbound payload for user credentials login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Nested user summary in login response.</summary>
public sealed record UserDto(Guid Id, string Email, string Role);

/// <summary>Outbound 200 response for successful login / token refresh.</summary>
public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, UserDto User);

/// <summary>Inbound payload for password change.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Outbound response for user profile retrieval.</summary>
public sealed record UserProfileResponse(Guid Id, string Email, string Role, DateTimeOffset CreatedAt);

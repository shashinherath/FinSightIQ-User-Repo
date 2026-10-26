using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Application service orchestrating user authentication, profile, and administration workflows.
/// </summary>
public interface IUserService
{
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<(LoginResponse Response, string RawRefreshToken)>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<(LoginResponse Response, string RawRefreshToken)>> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<PagedUsersResponse>> GetUsersAsync(int limit, int offset, CancellationToken cancellationToken = default);
    Task<Result> SuspendUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ReinstateUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

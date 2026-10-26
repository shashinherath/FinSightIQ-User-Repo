using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Data persistence operations for refresh token records and family tracking.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshTokenRecordDto?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task CreateAsync(RefreshTokenRecordDto token, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RotateTokenAsync(Guid oldTokenId, RefreshTokenRecordDto newToken, CancellationToken cancellationToken = default);
}

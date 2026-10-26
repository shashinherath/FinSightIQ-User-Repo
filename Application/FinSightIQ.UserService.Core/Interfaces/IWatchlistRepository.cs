using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Data persistence operations for user watchlists.
/// </summary>
public interface IWatchlistRepository
{
    Task<IReadOnlyList<WatchlistItemDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WatchlistItemDto> AddOrGetAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);
}

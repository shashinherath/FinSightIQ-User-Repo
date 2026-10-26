using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Application service managing analyst stock watchlists.
/// </summary>
public interface IWatchlistService
{
    Task<Result<WatchlistResponse>> GetWatchlistAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<WatchlistItemDto>> AddSymbolAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);
    Task<Result> RemoveSymbolAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);
}

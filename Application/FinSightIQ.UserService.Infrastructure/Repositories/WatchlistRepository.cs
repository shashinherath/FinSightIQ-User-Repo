using Dapper;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;

namespace FinSightIQ.UserService.Infrastructure.Repositories;

public class WatchlistRepository : IWatchlistRepository
{
    private readonly IDapperWrapper _dapper;

    public WatchlistRepository(IDapperWrapper dapper)
    {
        _dapper = dapper;
    }

    public async Task<IReadOnlyList<WatchlistItemDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_GetWatchlistByUserId(@userId)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("userId", userId);

        IEnumerable<WatchlistItemDto> items = await _dapper.QueryAsync<WatchlistItemDto>(sql, parameters);
        return items.ToList();
    }

    public async Task<WatchlistItemDto> AddOrGetAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_AddOrGetWatchlistItem(@id, @userId, @symbol, @addedAt)";
        Guid id = Guid.NewGuid();
        DateTimeOffset addedAt = DateTimeOffset.UtcNow;

        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", id);
        parameters.Add("userId", userId);
        parameters.Add("symbol", symbol);
        parameters.Add("addedAt", addedAt);

        WatchlistItemDto? existingOrNew = await _dapper.QuerySingleOrDefaultAsync<WatchlistItemDto>(sql, parameters);
        return existingOrNew ?? new WatchlistItemDto(id, userId, symbol, addedAt);
    }

    public async Task<bool> RemoveAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        bool exists = await ExistsAsync(userId, symbol, cancellationToken);
        if (!exists)
        {
            return false;
        }

        const string storedProcedure = "CALL sp_DeleteWatchlistItem(@userId, @symbol)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("userId", userId);
        parameters.Add("symbol", symbol);

        await _dapper.ExecuteAsync(storedProcedure, parameters);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_ExistsWatchlistItem(@userId, @symbol)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("userId", userId);
        parameters.Add("symbol", symbol);

        return await _dapper.QuerySingleOrDefaultAsync<bool>(sql, parameters);
    }
}

using Dapper;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;

namespace FinSightIQ.UserService.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDapperWrapper _dapper;

    public RefreshTokenRepository(IDapperWrapper dapper)
    {
        _dapper = dapper;
    }

    public async Task<RefreshTokenRecordDto?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_GetRefreshTokenByHash(@tokenHash)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("tokenHash", tokenHash);

        return await _dapper.QuerySingleOrDefaultAsync<RefreshTokenRecordDto>(sql, parameters);
    }

    public async Task CreateAsync(RefreshTokenRecordDto token, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT fn_CreateRefreshToken(" +
            "@id, " +
            "@userId, " +
            "@tokenHash, " +
            "@familyId, " +
            "@expiresAt, " +
            "@usedAt, " +
            "@revokedAt, " +
            "@createdAt)";

        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", token.Id);
        parameters.Add("userId", token.UserId);
        parameters.Add("tokenHash", token.TokenHash);
        parameters.Add("familyId", token.FamilyId);
        parameters.Add("expiresAt", token.ExpiresAt);
        parameters.Add("usedAt", token.UsedAt);
        parameters.Add("revokedAt", token.RevokedAt);
        parameters.Add("createdAt", token.CreatedAt);

        await _dapper.ExecuteAsync(sql, parameters);
    }

    public async Task MarkUsedAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        const string storedProcedure = "CALL sp_MarkRefreshTokenUsed(@id)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", tokenId);

        await _dapper.ExecuteAsync(storedProcedure, parameters);
    }

    public async Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        const string storedProcedure = "CALL sp_RevokeRefreshToken(@id)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", tokenId);

        await _dapper.ExecuteAsync(storedProcedure, parameters);
    }

    public async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        const string storedProcedure = "CALL sp_RevokeRefreshTokenFamily(@familyId)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("familyId", familyId);

        await _dapper.ExecuteAsync(storedProcedure, parameters);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string storedProcedure = "CALL sp_RevokeAllRefreshTokensForUser(@userId)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("userId", userId);

        await _dapper.ExecuteAsync(storedProcedure, parameters);
    }

    public async Task RotateTokenAsync(Guid oldTokenId, RefreshTokenRecordDto newToken, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT fn_RotateRefreshToken(" +
            "@oldTokenId, " +
            "@newId, " +
            "@userId, " +
            "@tokenHash, " +
            "@familyId, " +
            "@expiresAt, " +
            "@createdAt)";

        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("oldTokenId", oldTokenId);
        parameters.Add("newId", newToken.Id);
        parameters.Add("userId", newToken.UserId);
        parameters.Add("tokenHash", newToken.TokenHash);
        parameters.Add("familyId", newToken.FamilyId);
        parameters.Add("expiresAt", newToken.ExpiresAt);
        parameters.Add("createdAt", newToken.CreatedAt);

        int rowsUpdated = await _dapper.QuerySingleOrDefaultAsync<int>(sql, parameters);
        if (rowsUpdated == 0)
        {
            throw new InvalidOperationException("Refresh token was already used or revoked concurrently.");
        }
    }
}

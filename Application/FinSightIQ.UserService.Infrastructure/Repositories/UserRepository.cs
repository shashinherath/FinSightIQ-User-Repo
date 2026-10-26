using Dapper;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;

namespace FinSightIQ.UserService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDapperWrapper _dapper;

    public UserRepository(IDapperWrapper dapper)
    {
        _dapper = dapper;
    }

    public async Task<UserRecordDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_GetUserById(@id)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", id);

        return await _dapper.QuerySingleOrDefaultAsync<UserRecordDto>(sql, parameters);
    }

    public async Task<UserRecordDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_GetUserByEmail(@email)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("email", email);

        return await _dapper.QuerySingleOrDefaultAsync<UserRecordDto>(sql, parameters);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_ExistsByEmail(@email)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("email", email);

        return await _dapper.QuerySingleOrDefaultAsync<bool>(sql, parameters);
    }

    public async Task<UserRecordDto> CreateAsync(UserRecordDto user, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_CreateUser(" +
            "@id, " +
            "@email, " +
            "@passwordHash, " +
            "@role, " +
            "@isSuspended, " +
            "@createdAt, " +
            "@updatedAt, " +
            "@deletedAt)";

        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", user.Id);
        parameters.Add("email", user.Email);
        parameters.Add("passwordHash", user.PasswordHash);
        parameters.Add("role", user.Role.ToString());
        parameters.Add("isSuspended", user.IsSuspended);
        parameters.Add("createdAt", user.CreatedAt);
        parameters.Add("updatedAt", user.UpdatedAt);
        parameters.Add("deletedAt", user.DeletedAt);

        UserRecordDto? created = await _dapper.QuerySingleOrDefaultAsync<UserRecordDto>(sql, parameters);
        return created ?? user;
    }

    public async Task UpdateAsync(UserRecordDto user, CancellationToken cancellationToken = default)
    {
        const string storedProcedure = "CALL sp_UpdateUser(" +
            "@id, " +
            "@email, " +
            "@passwordHash, " +
            "@role, " +
            "@isSuspended, " +
            "@updatedAt, " +
            "@deletedAt)";

        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("id", user.Id);
        parameters.Add("email", user.Email);
        parameters.Add("passwordHash", user.PasswordHash);
        parameters.Add("role", user.Role.ToString());
        parameters.Add("isSuspended", user.IsSuspended);
        parameters.Add("updatedAt", user.UpdatedAt);
        parameters.Add("deletedAt", user.DeletedAt);

        await _dapper.ExecuteAsync(storedProcedure, parameters);
    }

    public async Task<IReadOnlyList<UserRecordDto>> GetPagedAsync(int limit, int offset, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_GetPagedUsers(@limit, @offset)";
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("limit", limit);
        parameters.Add("offset", offset);

        IEnumerable<UserRecordDto> users = await _dapper.QueryAsync<UserRecordDto>(sql, parameters);
        return users.ToList();
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM fn_GetUsersCount()";
        return await _dapper.QuerySingleOrDefaultAsync<int>(sql);
    }
}

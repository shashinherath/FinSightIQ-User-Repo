using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using FinSightIQ.UserService.Core.Interfaces;

namespace FinSightIQ.UserService.Infrastructure.Persistence;

public class DapperWrapper : IDapperWrapper
{
    private readonly string _connectionString;

    static DapperWrapper()
    {
        DapperConfiguration.Configure();
    }

    public DapperWrapper(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("UserDb") 
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'UserDb' or 'DefaultConnection' not found");
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout, commandType);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(sql, param, transaction, commandTimeout, commandType);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.ExecuteAsync(sql, param, transaction, commandTimeout, commandType);
    }
}

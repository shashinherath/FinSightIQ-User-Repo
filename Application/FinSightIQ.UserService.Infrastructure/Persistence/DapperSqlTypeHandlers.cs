using System.Data;
using Dapper;
using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Infrastructure.Persistence;

/// <summary>
/// Custom Dapper type handler to map PostgreSQL varchar role to UserRole enum.
/// </summary>
public sealed class UserRoleTypeHandler : SqlMapper.TypeHandler<UserRole>
{
    public override void SetValue(IDbDataParameter parameter, UserRole value)
    {
        parameter.Value = value.ToString();
    }

    public override UserRole Parse(object value)
    {
        if (value is string stringValue && Enum.TryParse<UserRole>(stringValue, true, out UserRole role))
        {
            return role;
        }

        if (value is int intValue && Enum.IsDefined(typeof(UserRole), intValue))
        {
            return (UserRole)intValue;
        }

        return UserRole.Analyst;
    }
}

/// <summary>
/// Configures Dapper global conventions.
/// </summary>
public static class DapperConfiguration
{
    private static bool _isConfigured;
    private static readonly object Lock = new();

    public static void Configure()
    {
        if (_isConfigured) return;

        lock (Lock)
        {
            if (_isConfigured) return;

            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.AddTypeHandler(new UserRoleTypeHandler());

            _isConfigured = true;
        }
    }
}

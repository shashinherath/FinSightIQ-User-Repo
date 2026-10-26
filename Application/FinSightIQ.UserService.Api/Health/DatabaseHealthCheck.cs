using FinSightIQ.UserService.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinSightIQ.UserService.Api.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDapperWrapper _dapper;

    public DatabaseHealthCheck(IDapperWrapper dapper)
    {
        _dapper = dapper;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int result = await _dapper.QuerySingleOrDefaultAsync<int>("SELECT 1;");

            return result == 1
                ? HealthCheckResult.Healthy("Database is responding.")
                : HealthCheckResult.Unhealthy("Database query returned unexpected result.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}

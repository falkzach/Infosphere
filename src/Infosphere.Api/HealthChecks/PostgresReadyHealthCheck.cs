using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Infosphere.Api.HealthChecks;

public sealed class PostgresReadyHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("Postgres is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Postgres is not reachable.", exception);
        }
    }
}

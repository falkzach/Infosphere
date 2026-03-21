using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infosphere.Api;

public sealed class StartupHealthCheck : IHealthCheck
{
    private volatile bool _startupCompleted;

    public void MarkStartupComplete()
    {
        _startupCompleted = true;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _startupCompleted
                ? HealthCheckResult.Healthy("Startup completed.")
                : HealthCheckResult.Unhealthy("Startup is still in progress."));
    }
}

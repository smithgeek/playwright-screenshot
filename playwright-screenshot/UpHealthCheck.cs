using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace playwright_screenshot;

public class UpHealthCheck : IHealthCheck
{
	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(HealthCheckResult.Healthy("Server Up"));
	}
}

using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevFlow.Identity.API.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IdentityDbContext _dbContext;

    public DatabaseHealthCheck(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
    }
}

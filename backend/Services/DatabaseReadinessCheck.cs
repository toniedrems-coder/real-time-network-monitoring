using System.Data.Common;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace backend.Services;

public sealed class DatabaseReadinessCheck : IHealthCheck
{
    private readonly MonitoringDbContext _dbContext;
    private readonly ILogger<DatabaseReadinessCheck> _logger;

    public DatabaseReadinessCheck(
        MonitoringDbContext dbContext,
        ILogger<DatabaseReadinessCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Monitoring database is not reachable.");
            }

            var knownMigrations = _dbContext.Database.GetMigrations();
            var appliedMigrations = await _dbContext.Database
                .GetAppliedMigrationsAsync(cancellationToken);
            var pendingCount = knownMigrations
                .Except(appliedMigrations, StringComparer.Ordinal)
                .Count();
            return pendingCount == 0
                ? HealthCheckResult.Healthy("Monitoring database is reachable and up to date.")
                : HealthCheckResult.Unhealthy(
                    $"Monitoring database has {pendingCount} pending migration(s).");
        }
        catch (DbException exception)
        {
            _logger.LogError(exception, "Monitoring database readiness check failed.");
            return HealthCheckResult.Unhealthy("Monitoring database readiness check failed.");
        }
    }
}

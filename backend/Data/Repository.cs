using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public interface IMetricsRepository
{
    Task AddAsync(
        EndpointMetrics metrics,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EndpointMetrics>> GetHistoryAsync(
        Guid endpointId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}

public sealed class MetricsRepository : IMetricsRepository
{
    private readonly MonitoringDbContext dbContext;

    public MetricsRepository(MonitoringDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task AddAsync(
        EndpointMetrics metrics,
        CancellationToken cancellationToken)
    {
        // Kafka is at-least-once.
        // The metric Id acts as our idempotency key.
        var exists = await dbContext.EndpointMetrics
            .AnyAsync(x => x.Id == metrics.Id, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.EndpointMetrics.Add(metrics);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EndpointMetrics>> GetHistoryAsync(
        Guid endpointId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EndpointMetrics
            .AsNoTracking()
            .Where(x => x.EndpointId == endpointId);

        if (from.HasValue)
        {
            query = query.Where(x => x.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.Timestamp <= to.Value);
        }

        return await query
            .OrderBy(x => x.Timestamp)
            .ToListAsync(cancellationToken);
    }
}


public interface IAnomalyRepository
{
    Task AddAsync(
        DetectedAnomaly anomaly,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DetectedAnomaly>> GetForEndpointAsync(
        Guid endpointId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}

public sealed class AnomalyRepository : IAnomalyRepository
{
    private readonly MonitoringDbContext dbContext;

    public AnomalyRepository(MonitoringDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task AddAsync(
        DetectedAnomaly anomaly,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.DetectedAnomalies
            .AnyAsync(x => x.Id == anomaly.Id, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.DetectedAnomalies.Add(anomaly);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetForEndpointAsync(
        Guid endpointId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DetectedAnomalies
            .AsNoTracking()
            .Where(x => x.EndpointId == endpointId);

        if (from.HasValue)
        {
            query = query.Where(x => x.DetectedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.DetectedAt <= to.Value);
        }

        return await query
            .OrderByDescending(x => x.DetectedAt)
            .ToListAsync(cancellationToken);
    }
}

public interface IKpiSnapshotRepository
{
    Task AddAsync(
        KpiSnapshot snapshot,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<KpiSnapshot>> GetHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}

public sealed class KpiSnapshotRepository : IKpiSnapshotRepository
{
    private readonly MonitoringDbContext dbContext;

    public KpiSnapshotRepository(
        MonitoringDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task AddAsync(
        KpiSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        dbContext.KpiSnapshots.Add(snapshot);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<KpiSnapshot>> GetHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.KpiSnapshots
            .AsNoTracking()
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(
                x => x.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(
                x => x.Timestamp <= to.Value);
        }

        return await query
            .OrderBy(x => x.Timestamp)
            .ToListAsync(cancellationToken);
    }
}


public interface IEndpointRepository
{
    Task<IReadOnlyList<MonitoredEndpoint>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<MonitoredEndpoint?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task AddAsync(
        MonitoredEndpoint endpoint,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        MonitoredEndpoint endpoint,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}


public sealed class EndpointRepository : IEndpointRepository
{
    private readonly MonitoringDbContext dbContext;

    public EndpointRepository(MonitoringDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MonitoredEndpoint>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.MonitoredEndpoints
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<MonitoredEndpoint?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.MonitoredEndpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        MonitoredEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        dbContext.MonitoredEndpoints.Add(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        MonitoredEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        dbContext.MonitoredEndpoints.Update(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.MonitoredEndpoints
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (endpoint is null)
        {
            return;
        }

        dbContext.MonitoredEndpoints.Remove(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}


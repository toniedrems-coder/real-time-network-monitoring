using backend.Data;

namespace backend.Services;

/// <summary>
/// Periodically snapshots system-wide KPIs and stores them
/// in both PostgreSQL and the in-memory KPI history store.
/// </summary>
public sealed class KpiSnapshotWorker : BackgroundService
{
    private static readonly TimeSpan SnapshotInterval =
        TimeSpan.FromMinutes(5);

    private readonly ReportingService reportingService;
    private readonly KpiHistoryStore kpiHistoryStore;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<KpiSnapshotWorker> logger;

    public KpiSnapshotWorker(
        ReportingService reportingService,
        KpiHistoryStore kpiHistoryStore,
        IServiceScopeFactory scopeFactory,
        ILogger<KpiSnapshotWorker> logger)
    {
        this.reportingService = reportingService;
        this.kpiHistoryStore = kpiHistoryStore;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot =
                    reportingService.BuildKpiSnapshot();

                // Persist first.
                using (var scope = scopeFactory.CreateScope())
                {
                    var repository =
                        scope.ServiceProvider
                            .GetRequiredService<IKpiSnapshotRepository>();

                    await repository.AddAsync(
                        snapshot,
                        stoppingToken);
                }

                // Keep recent history in memory.
                kpiHistoryStore.Record(snapshot);

                logger.LogInformation(
                    "KPI snapshot recorded at {Timestamp}.",
                    snapshot.Timestamp);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to record KPI snapshot.");
            }

            try
            {
                await Task.Delay(
                    SnapshotInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
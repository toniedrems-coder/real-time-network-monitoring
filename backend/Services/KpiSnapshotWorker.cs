namespace backend.Services;

/// <summary>
/// Periodically snapshots system-wide KPIs (healthy %, latency percentiles, anomaly
/// rate, endpoints at risk) into <see cref="KpiHistoryStore"/> so the dashboard can
/// render historical trend charts, not just the current live values.
/// </summary>
public sealed class KpiSnapshotWorker : BackgroundService
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(5);

    private readonly ReportingService reportingService;
    private readonly KpiHistoryStore kpiHistoryStore;
    private readonly ILogger<KpiSnapshotWorker> logger;

    public KpiSnapshotWorker(
        ReportingService reportingService,
        KpiHistoryStore kpiHistoryStore,
        ILogger<KpiSnapshotWorker> logger)
    {
        this.reportingService = reportingService;
        this.kpiHistoryStore = kpiHistoryStore;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                kpiHistoryStore.Record(reportingService.BuildKpiSnapshot());
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to record KPI snapshot.");
            }

            try
            {
                await Task.Delay(SnapshotInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

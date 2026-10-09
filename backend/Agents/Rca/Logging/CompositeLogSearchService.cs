namespace backend.Agents.Rca.Logging;

public sealed class CompositeLogSearchService : ILogSearchService
{
    private readonly InMemoryLogSearchService _memory;
    private readonly LokiLogSearchService _loki;

    public CompositeLogSearchService(
        InMemoryLogSearchService memory,
        LokiLogSearchService loki)
    {
        _memory = memory;
        _loki = loki;
    }

    public async Task<IReadOnlyCollection<LogEntry>> SearchAsync(
        LogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var entries = await _memory.SearchAsync(request, cancellationToken);
        var externalEntries = await _loki.SearchAsync(request, cancellationToken);
        return entries
            .Concat(externalEntries)
            .DistinctBy(x => (x.Timestamp, x.Source, x.Message, x.TraceId))
            .OrderByDescending(x => x.Timestamp)
            .Take(Math.Clamp(request.MaximumResults, 1, 1000))
            .ToArray();
    }
}

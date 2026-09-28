namespace backend.Agents.Rca.Logging;

public sealed class InMemoryLogSearchService
    : ILogSearchService
{
    private readonly List<LogEntry> entries = new();
    private readonly object sync = new();

    public void Add(LogEntry entry)
    {
        lock (sync)
        {
            entries.Add(entry);

            // Prevent unlimited growth during development.
            if (entries.Count > 5000)
            {
                entries.RemoveRange(
                    0,
                    entries.Count - 5000);
            }
        }
    }

    public Task<IReadOnlyCollection<LogEntry>> SearchAsync(
        LogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<LogEntry> snapshot;

        lock (sync)
        {
            snapshot = entries.ToList();
        }

        var query =
            snapshot
                .Where(x =>
                    x.Timestamp >= request.From &&
                    x.Timestamp <= request.To);

        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            var requestedTarget =
                NormalizeTarget(request.Target);

            query =
                query.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Target) &&
                    string.Equals(
                        NormalizeTarget(x.Target),
                        requestedTarget,
                        StringComparison.OrdinalIgnoreCase));
        }

        if (request.Levels.Count > 0)
        {
            query =
                query.Where(x =>
                    request.Levels.Contains(
                        x.Level,
                        StringComparer.OrdinalIgnoreCase));
        }

        var results =
            query
                .OrderByDescending(x => x.Timestamp)
                .Take(request.MaximumResults)
                .ToList();

        return Task.FromResult<
            IReadOnlyCollection<LogEntry>>(results);
    }

    private static string NormalizeTarget(
    string target)
    {
        return target
            .Trim()
            .TrimEnd('/');
    }
}
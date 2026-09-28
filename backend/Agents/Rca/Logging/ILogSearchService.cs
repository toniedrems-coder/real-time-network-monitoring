namespace backend.Agents.Rca.Logging;

public interface ILogSearchService
{
    Task<IReadOnlyCollection<LogEntry>> SearchAsync(
        LogSearchRequest request,
        CancellationToken cancellationToken = default);
}
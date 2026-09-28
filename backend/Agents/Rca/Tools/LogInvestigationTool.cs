using backend.Agents.Rca.Logging;
using backend.Agents.Rca.Models;
using backend.Models;

namespace backend.Agents.Rca.Tools;

public sealed class LogInvestigationTool
    : IInvestigationTool
{
    private readonly ILogSearchService logSearchService;
    private readonly ILogger<LogInvestigationTool> logger;

    public LogInvestigationTool(
        ILogSearchService logSearchService,
        ILogger<LogInvestigationTool> logger)
    {
        this.logSearchService = logSearchService;
        this.logger = logger;
    }

    public string Name => "Log Investigation Tool";

    public string Description =>
        "Searches operational logs around an incident and identifies " +
        "errors, exceptions, repeated patterns and correlation identifiers.";

    public bool CanInvestigate(Incident incident)
    {
        return !string.IsNullOrWhiteSpace(incident.Target);
    }

    public async Task<IReadOnlyCollection<RcaEvidence>>
        InvestigateAsync(
            Incident incident,
            CancellationToken cancellationToken = default)
    {
        var evidence =
            new List<RcaEvidence>();

        var from =
            incident.DetectedAt.AddMinutes(-5);

        var to =
            DateTimeOffset.UtcNow.AddMinutes(1);

        logger.LogInformation(
            "Searching logs for incident {IncidentNumber} " +
            "between {From} and {To}.",
            incident.IncidentNumber,
            from,
            to);

        var logs =
            await logSearchService.SearchAsync(
                new LogSearchRequest
                {
                    Target = incident.Target,
                    From = from,
                    To = to,
                    MaximumResults = 100
                },
                cancellationToken);

        if (logs.Count == 0)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = Name,
                    Type = "LogSearch",
                    Description =
                        "No correlated operational logs were found.",
                    Value =
                        $"Target={incident.Target}"
                });

            return evidence;
        }

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "LogCount",
                Description =
                    "Number of correlated log entries found.",
                Value =
                    logs.Count.ToString()
            });

        var errors =
            logs.Where(x =>
                    string.Equals(
                        x.Level,
                        "Error",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        x.Level,
                        "Critical",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "ErrorLogCount",
                Description =
                    "Number of error or critical logs in the incident window.",
                Value =
                    errors.Count.ToString()
            });

        AddExceptionEvidence(
            errors,
            evidence);

        AddRepeatedErrorEvidence(
            errors,
            evidence);

        AddCorrelationEvidence(
            logs,
            evidence);

        AddRecentErrors(
            errors,
            evidence);

        return evidence;
    }

    private void AddExceptionEvidence(
        IReadOnlyCollection<LogEntry> errors,
        List<RcaEvidence> evidence)
    {
        var exceptionTypes =
            errors
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.ExceptionType))
                .GroupBy(x => x.ExceptionType!)
                .OrderByDescending(x => x.Count())
                .Select(x =>
                    $"{x.Key}={x.Count()}")
                .ToList();

        if (exceptionTypes.Count == 0)
        {
            return;
        }

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "ExceptionTypes",
                Description =
                    "Exception types found in correlated logs.",
                Value =
                    string.Join(", ", exceptionTypes)
            });
    }

    private void AddRepeatedErrorEvidence(
        IReadOnlyCollection<LogEntry> errors,
        List<RcaEvidence> evidence)
    {
        var repeated =
            errors
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Message))
                .GroupBy(x => x.Message)
                .Where(x => x.Count() >= 2)
                .OrderByDescending(x => x.Count())
                .Take(5)
                .Select(x =>
                    $"{x.Count()}x: {x.Key}")
                .ToList();

        if (repeated.Count == 0)
        {
            return;
        }

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "RepeatedErrors",
                Description =
                    "Repeated error patterns found during the incident window.",
                Value =
                    string.Join(" | ", repeated)
            });
    }

    private void AddCorrelationEvidence(
        IReadOnlyCollection<LogEntry> logs,
        List<RcaEvidence> evidence)
    {
        var correlationIds =
            logs
                .Select(x =>
                    x.CorrelationId ?? x.TraceId)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(10)
                .ToList();

        if (correlationIds.Count == 0)
        {
            return;
        }

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "CorrelationIds",
                Description =
                    "Correlation or trace identifiers associated with the incident.",
                Value =
                    string.Join(", ", correlationIds)
            });
    }

    private void AddRecentErrors(
        IReadOnlyCollection<LogEntry> errors,
        List<RcaEvidence> evidence)
    {
        var recentErrors =
            errors
                .OrderByDescending(x => x.Timestamp)
                .Take(5)
                .Select(x =>
                    $"{x.Timestamp:O} [{x.Level}] " +
                    $"{x.Source}: {x.Message}")
                .ToList();

        if (recentErrors.Count == 0)
        {
            return;
        }

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "RecentErrors",
                Description =
                    "Most recent correlated error messages.",
                Value =
                    string.Join(" | ", recentErrors)
            });
    }
}
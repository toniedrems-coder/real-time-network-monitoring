using backend.Agents.Abstractions;
using backend.Agents.Knowledge.Models;
using backend.Agents.Models;
using backend.Models;
using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Agents.Knowledge;

public sealed class KnowledgeAgent : IAiOpsAgent
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<KnowledgeAgent> _logger;

    public KnowledgeAgent(
        IServiceScopeFactory scopeFactory,
        ILogger<KnowledgeAgent> logger)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;
    }

    public string Id =>
        "knowledge-agent";

    public string Name =>
        "Knowledge Agent";

    public string Description =>
        "Searches operational knowledge for previous incidents, resolutions and runbooks relevant to the current incident.";

    public AgentStatus Status { get; private set; }
        = AgentStatus.Stopped;

    public bool Enabled => true;

    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt =
            DateTimeOffset.UtcNow;

        try
        {
            Status =
                AgentStatus.Investigating;

            if (string.IsNullOrWhiteSpace(
                    context.IncidentId))
            {
                return AgentResult.Failed(
                    Name,
                    "IncidentId is required.",
                    startedAt);
            }

            if (!Guid.TryParse(
                    context.IncidentId,
                    out var incidentId))
            {
                return AgentResult.Failed(
                    Name,
                    "IncidentId is not a valid GUID.",
                    startedAt);
            }

            using var scope =
                _scopeFactory.CreateScope();

            var incidentService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IncidentService>();

            var matchingService =
                scope.ServiceProvider
                    .GetRequiredService<
                        KnowledgeMatchingService>();

            var knowledgeBaseService =
                scope.ServiceProvider
                    .GetRequiredService<
                        KnowledgeBaseService>();

            var incident =
                await incidentService.GetByIdAsync(
                    incidentId,
                    cancellationToken);

            if (incident is null)
            {
                return AgentResult.Failed(
                    Name,
                    $"Incident {incidentId} was not found.",
                    startedAt);
            }

            _logger.LogInformation(
                "Knowledge Agent searching operational knowledge for incident {IncidentNumber}.",
                incident.IncidentNumber);

            var result =
                await matchingService.SearchAsync(
                    incident,
                    cancellationToken);

            if (result.BestMatch is not null)
            {
                await knowledgeBaseService
                    .RecordMatchAsync(
                        result.BestMatch.ArticleId,
                        cancellationToken);
            }

            var message =
                result.MatchFound
                    ? $"{result.MatchCount} relevant knowledge article(s) found for incident {incident.IncidentNumber}."
                    : $"No relevant operational knowledge was found for incident {incident.IncidentNumber}.";

            return AgentResult.Successful(
    Name,
    message,
    startedAt,
    new Dictionary<string, object>
    {
        ["incidentId"] =
            incident.Id,

        ["incidentNumber"] =
            incident.IncidentNumber,

        ["target"] =
            incident.Target,

        ["failureType"] =
            incident.FailureType.ToString(),

        ["matchFound"] =
            result.MatchFound,

        ["candidateCount"] =
            result.CandidateCount,

        ["matchCount"] =
            result.MatchCount,

        ["bestMatch"] =
            result.BestMatch!,

        ["matches"] =
            result.Matches,

        ["searchedAt"] =
            result.SearchedAt
    });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Status =
                AgentStatus.Failed;

            _logger.LogError(
                exception,
                "Knowledge Agent failed.");

            return AgentResult.Failed(
                Name,
                exception.Message,
                startedAt);
        }
        finally
        {
            if (Status !=
                AgentStatus.Failed)
            {
                Status =
                    AgentStatus.Stopped;
            }
        }
    }
}
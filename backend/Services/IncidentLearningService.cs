using backend.Agents.Knowledge.Models;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class IncidentLearningService
{
    private readonly MonitoringDbContext _dbContext;
    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly KnowledgeMatchingService _knowledgeMatchingService;
    private readonly ILogger<IncidentLearningService> _logger;

    public IncidentLearningService(
        MonitoringDbContext dbContext,
        KnowledgeBaseService knowledgeBaseService,
        KnowledgeMatchingService knowledgeMatchingService,
        ILogger<IncidentLearningService> logger)
    {
        _dbContext = dbContext;
        _knowledgeBaseService = knowledgeBaseService;
        _knowledgeMatchingService = knowledgeMatchingService;
        _logger = logger;
    }

    public async Task<IncidentLearningResult>
        LearnFromResolvedIncidentAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------
        // 1. Load incident
        // -------------------------------------------------

        var incident =
            await _dbContext.Incidents
                .FirstOrDefaultAsync(
                    x => x.Id == incidentId,
                    cancellationToken);

        if (incident is null)
        {
            return new IncidentLearningResult
            {
                IncidentId = incidentId,
                IncidentNumber = "Unknown",
                Learned = false,
                ExistingKnowledgeUpdated = false,
                NewKnowledgeCreated = false,
                Message = "Incident was not found."
            };
        }

        // -------------------------------------------------
        // 2. Incident must be resolved
        // -------------------------------------------------

        if (incident.Status != IncidentStatus.Resolved)
        {
            return new IncidentLearningResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                Learned = false,
                ExistingKnowledgeUpdated = false,
                NewKnowledgeCreated = false,
                Message =
                    "Incident must be resolved before learning can occur."
            };
        }

        // -------------------------------------------------
        // 3. We need RCA/root cause
        // -------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                incident.RootCause))
        {
            return new IncidentLearningResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                Learned = false,
                ExistingKnowledgeUpdated = false,
                NewKnowledgeCreated = false,
                Message =
                    "Incident does not contain a root cause."
            };
        }

        // -------------------------------------------------
        // 4. We need an actual resolution
        // -------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                incident.Resolution))
        {
            return new IncidentLearningResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                Learned = false,
                ExistingKnowledgeUpdated = false,
                NewKnowledgeCreated = false,
                Message =
                    "Incident does not contain a resolution."
            };
        }

        // -------------------------------------------------
        // 5. Search existing knowledge
        // -------------------------------------------------

        var searchResult =
            await _knowledgeMatchingService
                .SearchAsync(
                    incident,
                    cancellationToken);

        var bestMatch =
            searchResult.BestMatch;

        // -------------------------------------------------
        // 6. Existing strong match
        // -------------------------------------------------

        // We deliberately use a reasonably strong threshold
        // before treating existing knowledge as successful.
        if (bestMatch is not null &&
            bestMatch.MatchScore >= 0.65)
        {
            var article =
                await _dbContext.KnowledgeArticles
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            bestMatch.ArticleId,
                        cancellationToken);

            if (article is not null)
            {
                article.TimesSuccessful++;

                article.LastUsedAt =
                    DateTimeOffset.UtcNow;

                article.UpdatedAt =
                    DateTimeOffset.UtcNow;

                await _dbContext
                    .SaveChangesAsync(
                        cancellationToken);

                _logger.LogInformation(
                    "Incident {IncidentNumber} reinforced knowledge article {ArticleNumber}.",
                    incident.IncidentNumber,
                    article.ArticleNumber);

                return new IncidentLearningResult
                {
                    IncidentId =
                        incident.Id,

                    IncidentNumber =
                        incident.IncidentNumber,

                    Learned = true,

                    ExistingKnowledgeUpdated =
                        true,

                    NewKnowledgeCreated =
                        false,

                    KnowledgeArticleId =
                        article.Id,

                    ArticleNumber =
                        article.ArticleNumber,

                    ArticleType =
                        article.Type,

                    Message =
                        "Existing operational knowledge was reinforced by the resolved incident."
                };
            }
        }

        // -------------------------------------------------
        // 7. No suitable existing knowledge
        //
        // Create a new IncidentLearning article.
        // -------------------------------------------------

        var request =
            new CreateKnowledgeArticleRequest
            {
                Title =
                    $"Incident learning: {incident.Title}",

                Description =
                    $"Operational learning generated from resolved incident {incident.IncidentNumber}.",

                Type =
                    KnowledgeArticleType.IncidentLearning,

                Target =
                    incident.Target,

                TargetType =
                    incident.TargetType,

                FailureType =
                    incident.FailureType.ToString(),

                RootCause =
                    incident.RootCause,

                Resolution =
                    incident.Resolution,

                RecommendedActions =
                    incident.RecommendedAction,

                SourceIncidentId =
                    incident.Id,

                Tags =
                    BuildTags(incident),

                Confidence =
                    0.80
            };

        var createdArticle =
            await _knowledgeBaseService
                .CreateAsync(
                    request,
                    cancellationToken);

        _logger.LogInformation(
            "Created knowledge article {ArticleNumber} from resolved incident {IncidentNumber}.",
            createdArticle.ArticleNumber,
            incident.IncidentNumber);

        // -------------------------------------------------
        // 8. Return learning result
        // -------------------------------------------------

        return new IncidentLearningResult
        {
            IncidentId =
                incident.Id,

            IncidentNumber =
                incident.IncidentNumber,

            Learned = true,

            ExistingKnowledgeUpdated =
                false,

            NewKnowledgeCreated =
                true,

            KnowledgeArticleId =
                createdArticle.Id,

            ArticleNumber =
                createdArticle.ArticleNumber,

            ArticleType =
                createdArticle.Type,

            Message =
                "New operational knowledge was created from the resolved incident."
        };
    }

    private static string BuildTags(
        Incident incident)
    {
        var tags =
            new List<string>
            {
                "incident-learning",
                incident.FailureType
                    .ToString()
                    .ToLowerInvariant()
            };

        if (!string.IsNullOrWhiteSpace(
                incident.TargetType))
        {
            tags.Add(
                incident.TargetType
                    .ToLowerInvariant());
        }

        return string.Join(
            ",",
            tags.Distinct());
    }
}
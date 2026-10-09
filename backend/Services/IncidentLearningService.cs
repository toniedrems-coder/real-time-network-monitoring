using backend.Agents.Knowledge.Models;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class IncidentLearningService
{
    private readonly MonitoringDbContext _dbContext;
    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<IncidentLearningService> _logger;
    private readonly IncidentKnowledgeMatchService _matchService;

    public IncidentLearningService(
        MonitoringDbContext dbContext,
        KnowledgeBaseService knowledgeBaseService,
        IncidentKnowledgeMatchService matchService,
        ILogger<IncidentLearningService> logger)
    {
        _dbContext = dbContext;
        _knowledgeBaseService = knowledgeBaseService;
        _matchService = matchService;
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

        // IMPORTANT:
        // Null check must happen BEFORE incident.Id is used.
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
        // 3. Root cause is required
        // -------------------------------------------------

        if (string.IsNullOrWhiteSpace(incident.RootCause))
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
        // 4. Resolution is required
        // -------------------------------------------------

        if (string.IsNullOrWhiteSpace(incident.Resolution))
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
        // 5. Load the ACTUAL persisted selected KB match
        // -------------------------------------------------

        var selectedMatch =
            await _matchService
                .GetSelectedMatchAsync(
                    incident.Id,
                    cancellationToken);

        // -------------------------------------------------
        // 6. Selected knowledge was verified successful
        // -------------------------------------------------

        if (selectedMatch is not null &&
            selectedMatch.WasSuccessful == true &&
            selectedMatch.KnowledgeArticle is not null)
        {
            var article =
                selectedMatch.KnowledgeArticle;

            var alreadyApplied =
                selectedMatch.LearningAppliedAt.HasValue;

            if (!alreadyApplied)
            {
                article.TimesSuccessful++;

                article.LastUsedAt =
                    DateTimeOffset.UtcNow;

                article.UpdatedAt =
                    DateTimeOffset.UtcNow;

                selectedMatch.LearningAppliedAt =
                    DateTimeOffset.UtcNow;

                selectedMatch.UpdatedAt =
                    DateTimeOffset.UtcNow;

                await _dbContext
                    .SaveChangesAsync(
                        cancellationToken);

                _logger.LogInformation(
                    "Incident {IncidentNumber} successfully reinforced knowledge article {ArticleNumber}.",
                    incident.IncidentNumber,
                    article.ArticleNumber);
            }
            else
            {
                _logger.LogInformation(
                    "Learning was already applied for incident {IncidentNumber} and knowledge article {ArticleNumber}.",
                    incident.IncidentNumber,
                    article.ArticleNumber);
            }

            return new IncidentLearningResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.IncidentNumber,

                Learned = true,

                // True only when this invocation changed the KB.
                ExistingKnowledgeUpdated = !alreadyApplied,

                NewKnowledgeCreated = false,

                KnowledgeArticleId = article.Id,
                ArticleNumber = article.ArticleNumber,
                ArticleType = article.Type,

                Message =
                    alreadyApplied
                        ? "Learning had already been applied for this incident."
                        : "Verified knowledge was reinforced from the resolved incident."
            };
        }

        // -------------------------------------------------
        // 7. Selected knowledge explicitly failed
        // -------------------------------------------------

        if (selectedMatch is not null &&
            selectedMatch.WasSuccessful == false)
        {
            _logger.LogInformation(
                "Knowledge article {KnowledgeArticleId} did not resolve incident {IncidentNumber}. Capturing the actual resolution as new learning.",
                selectedMatch.KnowledgeArticleId,
                incident.IncidentNumber);

            // Do NOT return.
            // Continue below and capture the actual resolution.
        }

        // -------------------------------------------------
        // 8. Prevent duplicate IncidentLearning articles
        // -------------------------------------------------

        var existingLearning =
            await _dbContext.KnowledgeArticles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.SourceIncidentId == incident.Id &&
                        x.Type ==
                            KnowledgeArticleType.IncidentLearning,
                    cancellationToken);

        if (existingLearning is not null)
        {
            return new IncidentLearningResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                Learned = true,

                ExistingKnowledgeUpdated = false,
                NewKnowledgeCreated = false,

                KnowledgeArticleId =
                    existingLearning.Id,

                ArticleNumber =
                    existingLearning.ArticleNumber,

                ArticleType =
                    existingLearning.Type,

                Message =
                    "Incident learning has already been captured."
            };
        }

        // -------------------------------------------------
        // 9. No verified successful existing knowledge.
        //    Capture actual incident resolution.
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
        // 10. Return learning result
        // -------------------------------------------------

        return new IncidentLearningResult
        {
            IncidentId = incident.Id,
            IncidentNumber = incident.IncidentNumber,
            Learned = true,

            ExistingKnowledgeUpdated = false,
            NewKnowledgeCreated = true,

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
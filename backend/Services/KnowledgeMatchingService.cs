using backend.Agents.Knowledge.Models;
using backend.Models;

namespace backend.Services;

public sealed class KnowledgeMatchingService
{
    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<KnowledgeMatchingService> _logger;

    public KnowledgeMatchingService(
        KnowledgeBaseService knowledgeBaseService,
        ILogger<KnowledgeMatchingService> logger)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _logger = logger;
    }

    public async Task<KnowledgeSearchResult> SearchAsync(
        Incident incident,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var failureType =
            incident.FailureType.ToString();

        /*
         * First retrieve active candidates.
         *
         * Target is deliberately not supplied here.
         * Exact target matching contributes to ranking,
         * but we also want reusable knowledge from other
         * targets with the same failure type.
         */
        var candidates =
            await _knowledgeBaseService.SearchAsync(
                target: null,
                failureType: failureType,
                type: null,
                cancellationToken: cancellationToken);

        var matches = candidates
            .Select(article =>
                ScoreArticle(
                    incident,
                    article))
            .Where(match =>
                match.MatchScore >= 0.30)
            .OrderByDescending(match =>
                match.MatchScore)
            .ThenByDescending(match =>
                match.ArticleConfidence)
            .Take(10)
            .ToList();

        var bestMatch =
            matches.FirstOrDefault();

        _logger.LogInformation(
            "Knowledge search for incident {IncidentNumber} produced {CandidateCount} candidates and {MatchCount} matches.",
            incident.IncidentNumber,
            candidates.Count,
            matches.Count);

        return new KnowledgeSearchResult
        {
            IncidentId = incident.Id,
            IncidentNumber =
                incident.IncidentNumber,

            Target = incident.Target,

            FailureType = failureType,

            CandidateCount =
                candidates.Count,

            MatchCount =
                matches.Count,

            MatchFound =
                bestMatch is not null,

            BestMatch =
                bestMatch,

            Matches =
                matches,

            SearchedAt =
                DateTimeOffset.UtcNow
        };
    }

    private static KnowledgeMatch ScoreArticle(
        Incident incident,
        KnowledgeArticle article)
    {
        var score = 0.0;

        var reasons =
            new List<string>();

        // -----------------------------------------
        // Failure type - strongest deterministic
        // signal.
        // -----------------------------------------

        if (EqualsIgnoreCase(
                article.FailureType,
                incident.FailureType.ToString()))
        {
            score += 0.35;

            reasons.Add(
                "Failure type matches the incident.");
        }

        // -----------------------------------------
        // Target
        // -----------------------------------------

        if (TargetsMatch(
                article.Target,
                incident.Target))
        {
            score += 0.25;

            reasons.Add(
                "Target matches the affected resource.");
        }

        // -----------------------------------------
        // Target type
        // -----------------------------------------

        if (EqualsIgnoreCase(
                article.TargetType,
                incident.TargetType))
        {
            score += 0.10;

            reasons.Add(
                "Target type matches.");
        }

        // -----------------------------------------
        // RCA/root cause
        // -----------------------------------------

        if (HasMeaningfulOverlap(
                article.RootCause,
                incident.RootCause))
        {
            score += 0.15;

            reasons.Add(
                "Root cause is similar to the incident RCA.");
        }

        // -----------------------------------------
        // Historical article confidence
        // -----------------------------------------

        var confidence =
            Math.Clamp(
                article.Confidence,
                0,
                1);

        score += confidence * 0.10;

        if (confidence >= 0.75)
        {
            reasons.Add(
                "Knowledge article has high historical confidence.");
        }

        // -----------------------------------------
        // Previous successful usage
        // -----------------------------------------

        if (article.TimesSuccessful > 0)
        {
            var successBonus =
                Math.Min(
                    article.TimesSuccessful * 0.01,
                    0.05);

            score += successBonus;

            reasons.Add(
                $"Previously successful {article.TimesSuccessful} time(s).");
        }

        score =
            Math.Clamp(
                score,
                0,
                1);

        return new KnowledgeMatch
        {
            ArticleId =
                article.Id,

            ArticleNumber =
                article.ArticleNumber,

            Title =
                article.Title,

            Type =
                article.Type,

            Target =
                article.Target,

            FailureType =
                article.FailureType,

            RootCause =
                article.RootCause,

            Resolution =
                article.Resolution,

            RecommendedActions =
                article.RecommendedActions,

            RunbookReference =
                article.RunbookReference,

            Tags =
                article.Tags,

            ArticleConfidence =
                confidence,

            MatchScore =
                score,

            MatchLevel =
                GetMatchLevel(score),

            MatchReasons =
                reasons
        };
    }

    private static bool TargetsMatch(
        string? left,
        string? right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var normalizedLeft =
            left.Trim()
                .TrimEnd('/');

        var normalizedRight =
            right.Trim()
                 .TrimEnd('/');

        return string.Equals(
            normalizedLeft,
            normalizedRight,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsIgnoreCase(
        string? left,
        string? right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            left.Trim(),
            right.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMeaningfulOverlap(
        string? left,
        string? right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftTokens =
            Tokenize(left);

        var rightTokens =
            Tokenize(right);

        if (leftTokens.Count == 0 ||
            rightTokens.Count == 0)
        {
            return false;
        }

        var overlap =
            leftTokens.Intersect(
                rightTokens,
                StringComparer.OrdinalIgnoreCase)
            .Count();

        var denominator =
            Math.Min(
                leftTokens.Count,
                rightTokens.Count);

        if (denominator == 0)
        {
            return false;
        }

        var ratio =
            (double)overlap /
            denominator;

        return ratio >= 0.30;
    }

    private static HashSet<string> Tokenize(
        string value)
    {
        var separators =
            new[]
            {
                ' ', ',', '.', ';', ':',
                '-', '_', '/', '\\',
                '(', ')', '[', ']',
                '\r', '\n', '\t'
            };

        return value
            .Split(
                separators,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(token =>
                token.Length >= 3)
            .Select(token =>
                token.ToLowerInvariant())
            .ToHashSet();
    }

    private static string GetMatchLevel(
        double score)
    {
        return score switch
        {
            >= 0.80 => "VeryHigh",
            >= 0.65 => "High",
            >= 0.45 => "Medium",
            _ => "Low"
        };
    }
}
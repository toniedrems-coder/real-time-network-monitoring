using backend.Agents.Knowledge.Models;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class IncidentKnowledgeMatchService
{
    private readonly MonitoringDbContext _dbContext;
    private readonly ILogger<IncidentKnowledgeMatchService> _logger;

    public IncidentKnowledgeMatchService(
        MonitoringDbContext dbContext,
        ILogger<IncidentKnowledgeMatchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistMatchesAsync(
        Guid incidentId,
        IReadOnlyCollection<KnowledgeMatch> matches,
        Guid? selectedArticleId,
        CancellationToken cancellationToken = default)
    {
        if (matches.Count == 0)
        {
            return;
        }

        var now =
            DateTimeOffset.UtcNow;

        var previouslySelected =
    await _dbContext
        .IncidentKnowledgeMatches
        .Where(
            x =>
                x.IncidentId ==
                    incidentId &&
                x.WasSelected)
        .ToListAsync(
            cancellationToken);

        foreach (var previous in
                 previouslySelected)
        {
            previous.WasSelected =
                false;

            previous.UpdatedAt =
                now;
        }

        foreach (var match in matches)
        {
            var existing =
                await _dbContext
                    .IncidentKnowledgeMatches
                    .FirstOrDefaultAsync(
                        x =>
                            x.IncidentId ==
                            incidentId &&
                            x.KnowledgeArticleId ==
                            match.ArticleId,
                        cancellationToken);

            var selected =
                selectedArticleId.HasValue &&
                selectedArticleId.Value ==
                match.ArticleId;

            var reasons =
                match.MatchReasons.Count > 0
                    ? string.Join(
                        " | ",
                        match.MatchReasons)
                    : null;

            if (existing is null)
            {
                var entity =
                    new IncidentKnowledgeMatch
                    {
                        Id = Guid.NewGuid(),

                        IncidentId =
                            incidentId,

                        KnowledgeArticleId =
                            match.ArticleId,

                        MatchScore =
                            match.MatchScore,

                        MatchLevel =
                            match.MatchLevel,

                        MatchReasons =
                            reasons,

                        WasSelected =
                            selected,

                        WasSuccessful =
                            null,

                        MatchedAt =
                            now,

                        CreatedAt =
                            now,

                        UpdatedAt =
                            now
                    };

                _dbContext
                    .IncidentKnowledgeMatches
                    .Add(entity);

                continue;
            }

            existing.MatchScore =
                match.MatchScore;

            existing.MatchLevel =
                match.MatchLevel;

            existing.MatchReasons =
                reasons;

            existing.WasSelected =
                selected;

            existing.MatchedAt =
                now;

            existing.UpdatedAt =
                now;

            /*
             * Do NOT reset:
             *
             * WasSuccessful
             * VerifiedAt
             *
             * Re-running the Knowledge Agent
             * shouldn't destroy historical
             * verification.
             */
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "Persisted {Count} knowledge matches for incident {IncidentId}. Selected article: {SelectedArticleId}.",
            matches.Count,
            incidentId,
            selectedArticleId);
    }

    public async Task<
        IReadOnlyCollection<IncidentKnowledgeMatch>>
        GetByIncidentIdAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .IncidentKnowledgeMatches
            .AsNoTracking()
            .Include(x => x.KnowledgeArticle)
            .Where(
                x =>
                    x.IncidentId ==
                    incidentId)
            .OrderByDescending(
                x => x.WasSelected)
            .ThenByDescending(
                x => x.MatchScore)
            .ToListAsync(
                cancellationToken);
    }

    public async Task<IncidentKnowledgeMatch?>
        GetSelectedMatchAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .IncidentKnowledgeMatches
            .Include(
                x => x.KnowledgeArticle)
            .FirstOrDefaultAsync(
                x =>
                    x.IncidentId ==
                    incidentId &&
                    x.WasSelected,
                cancellationToken);
    }

    public async Task MarkSuccessfulAsync(
        Guid incidentId,
        Guid knowledgeArticleId,
        bool successful,
        CancellationToken cancellationToken = default)
    {
        var match =
            await _dbContext
                .IncidentKnowledgeMatches
                .FirstOrDefaultAsync(
                    x =>
                        x.IncidentId ==
                        incidentId &&
                        x.KnowledgeArticleId ==
                        knowledgeArticleId,
                    cancellationToken);

        if (match is null)
        {
            return;
        }

        match.WasSuccessful =
            successful;

        match.VerifiedAt =
            DateTimeOffset.UtcNow;

        match.UpdatedAt =
            DateTimeOffset.UtcNow;

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    // public async Task<IncidentKnowledgeMatch?>
    // MarkSelectedMatchOutcomeAsync(
    //     Guid incidentId,
    //     bool successful,
    //     CancellationToken cancellationToken = default)
    // {
    //     var match =
    //         await _dbContext
    //             .IncidentKnowledgeMatches
    //             .Include(x => x.KnowledgeArticle)
    //             .FirstOrDefaultAsync(
    //                 x =>
    //                     x.IncidentId == incidentId &&
    //                     x.WasSelected,
    //                 cancellationToken);

    //     if (match is null)
    //     {
    //         return null;
    //     }

    //     var now =
    //         DateTimeOffset.UtcNow;

    //     match.WasSuccessful =
    //         successful;

    //     match.VerifiedAt =
    //         now;

    //     match.UpdatedAt =
    //         now;

    //     await _dbContext
    //         .SaveChangesAsync(
    //             cancellationToken);

    //     return match;
    // }

    public async Task MarkLearningAppliedAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match =
            await _dbContext
                .IncidentKnowledgeMatches
                .FirstOrDefaultAsync(
                    x => x.Id == matchId,
                    cancellationToken);

        //     var alreadyApplied =
        // selectedMatch
        //     .LearningAppliedAt
        //     .HasValue;

        if (match is null)
        {
            return;
        }

        if (match.LearningAppliedAt.HasValue)
        {
            return;
        }

        match.LearningAppliedAt =
            DateTimeOffset.UtcNow;

        match.UpdatedAt =
            DateTimeOffset.UtcNow;

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task<IncidentKnowledgeMatch?>
    MarkSelectedMatchOutcomeAsync(
        Guid incidentId,
        bool successful,
        CancellationToken cancellationToken = default)
    {
        var match =
            await _dbContext
                .IncidentKnowledgeMatches
                .Include(x => x.KnowledgeArticle)
                .FirstOrDefaultAsync(
                    x =>
                        x.IncidentId == incidentId &&
                        x.WasSelected,
                    cancellationToken);

        if (match is null)
        {
            _logger.LogInformation(
                "No selected knowledge match exists for incident {IncidentId}.",
                incidentId);

            return null;
        }

        // Don't overwrite an outcome that has already
        // been verified.
        if (match.VerifiedAt.HasValue)
        {
            _logger.LogInformation(
                "Knowledge match {MatchId} for incident {IncidentId} was already verified.",
                match.Id,
                incidentId);

            return match;
        }

        var now = DateTimeOffset.UtcNow;

        match.WasSuccessful = successful;
        match.VerifiedAt = now;
        match.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Knowledge match {MatchId} for incident {IncidentId} verified. Successful: {Successful}.",
            match.Id,
            incidentId,
            successful);

        return match;
    }

}
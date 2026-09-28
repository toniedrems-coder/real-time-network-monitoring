using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class KnowledgeBaseService
{
    private readonly MonitoringDbContext _dbContext;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        MonitoringDbContext dbContext,
        ILogger<KnowledgeBaseService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<KnowledgeArticle>>
        GetAllAsync(
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeArticles
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeArticle?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<KnowledgeArticle?> GetByArticleNumberAsync(
        string articleNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ArticleNumber == articleNumber,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<KnowledgeArticle>>
        SearchAsync(
            string? target,
            string? failureType,
            KnowledgeArticleType? type,
            CancellationToken cancellationToken = default)
    {
        var query = _dbContext.KnowledgeArticles
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(target))
        {
            var normalizedTarget = target
                .Trim()
                .TrimEnd('/');

            query = query.Where(
                x =>
                    x.Target != null &&
                    (
                        x.Target == target ||
                        x.Target == normalizedTarget ||
                        x.Target.StartsWith(
                            normalizedTarget)
                    ));
        }

        if (!string.IsNullOrWhiteSpace(failureType))
        {
            query = query.Where(
                x =>
                    x.FailureType != null &&
                    x.FailureType == failureType);
        }

        if (type.HasValue)
        {
            query = query.Where(
                x => x.Type == type.Value);
        }

        return await query
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.TimesSuccessful)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeArticle> CreateAsync(
        CreateKnowledgeArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException(
                "Knowledge article title is required.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException(
                "Knowledge article description is required.",
                nameof(request));
        }

        var now = DateTimeOffset.UtcNow;

        var article = new KnowledgeArticle
        {
            Id = Guid.NewGuid(),

            ArticleNumber =
                await GenerateArticleNumberAsync(
                    now,
                    cancellationToken),

            Title = request.Title.Trim(),

            Description =
                request.Description.Trim(),

            Type = request.Type,

            Target = NormalizeOptional(
                request.Target),

            TargetType = NormalizeOptional(
                request.TargetType),

            FailureType = NormalizeOptional(
                request.FailureType),

            RootCause = NormalizeOptional(
                request.RootCause),

            Resolution = NormalizeOptional(
                request.Resolution),

            RecommendedActions = NormalizeOptional(
                request.RecommendedActions),

            RunbookReference = NormalizeOptional(
                request.RunbookReference),

            Tags = NormalizeOptional(
                request.Tags),

            SourceIncidentId =
                request.SourceIncidentId,

            Confidence = Math.Clamp(
                request.Confidence,
                0,
                1),

            TimesMatched = 0,

            TimesSuccessful = 0,

            IsActive = true,

            CreatedAt = now,

            UpdatedAt = now
        };

        _dbContext.KnowledgeArticles.Add(article);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Knowledge article {ArticleNumber} created.",
            article.ArticleNumber);

        return article;
    }

    public async Task<KnowledgeArticle?> UpdateAsync(
        Guid id,
        UpdateKnowledgeArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var article =
            await _dbContext.KnowledgeArticles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (article is null)
        {
            return null;
        }

        if (request.Title is not null)
        {
            article.Title =
                request.Title.Trim();
        }

        if (request.Description is not null)
        {
            article.Description =
                request.Description.Trim();
        }

        if (request.Type.HasValue)
        {
            article.Type =
                request.Type.Value;
        }

        if (request.Target is not null)
        {
            article.Target =
                NormalizeOptional(
                    request.Target);
        }

        if (request.TargetType is not null)
        {
            article.TargetType =
                NormalizeOptional(
                    request.TargetType);
        }

        if (request.FailureType is not null)
        {
            article.FailureType =
                NormalizeOptional(
                    request.FailureType);
        }

        if (request.RootCause is not null)
        {
            article.RootCause =
                NormalizeOptional(
                    request.RootCause);
        }

        if (request.Resolution is not null)
        {
            article.Resolution =
                NormalizeOptional(
                    request.Resolution);
        }

        if (request.RecommendedActions is not null)
        {
            article.RecommendedActions =
                NormalizeOptional(
                    request.RecommendedActions);
        }

        if (request.RunbookReference is not null)
        {
            article.RunbookReference =
                NormalizeOptional(
                    request.RunbookReference);
        }

        if (request.Tags is not null)
        {
            article.Tags =
                NormalizeOptional(
                    request.Tags);
        }

        if (request.Confidence.HasValue)
        {
            article.Confidence =
                Math.Clamp(
                    request.Confidence.Value,
                    0,
                    1);
        }

        if (request.IsActive.HasValue)
        {
            article.IsActive =
                request.IsActive.Value;
        }

        article.UpdatedAt =
            DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Knowledge article {ArticleNumber} updated.",
            article.ArticleNumber);

        return article;
    }

    public async Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var article =
            await _dbContext.KnowledgeArticles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (article is null)
        {
            return false;
        }

        article.IsActive = false;
        article.UpdatedAt =
            DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private async Task<string> GenerateArticleNumberAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0;
             attempt < 5;
             attempt++)
        {
            var suffix = Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();

            var articleNumber =
                $"KB-{now:yyyyMMddHHmmss}-{suffix}";

            var exists =
                await _dbContext.KnowledgeArticles
                    .AnyAsync(
                        x =>
                            x.ArticleNumber ==
                            articleNumber,
                        cancellationToken);

            if (!exists)
            {
                return articleNumber;
            }
        }

        return $"KB-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    public async Task RecordMatchAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article =
            await _dbContext.KnowledgeArticles
                .FirstOrDefaultAsync(
                    x => x.Id == articleId,
                    cancellationToken);

        if (article is null)
        {
            return;
        }

        article.TimesMatched++;
        article.LastUsedAt =
            DateTimeOffset.UtcNow;

        article.UpdatedAt =
            DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
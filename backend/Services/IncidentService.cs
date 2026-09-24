using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class IncidentService
{
    private readonly MonitoringDbContext dbContext;
    private readonly ILogger<IncidentService> logger;

    public IncidentService(
        MonitoringDbContext dbContext,
        ILogger<IncidentService> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<Incident>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(x => x.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Incident?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<Incident> CreateOrUpdateAsync(
        CreateIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await dbContext.Incidents
                .FirstOrDefaultAsync(
                    x =>
                        x.SourceId == request.SourceId &&
                        x.FailureType == request.FailureType &&
                        x.Status != IncidentStatus.Resolved,
                    cancellationToken);

        if (existing is not null)
        {
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.DetectedAt = request.DetectedAt;
            existing.HttpStatusCode = request.HttpStatusCode;
            existing.LatencyMs = request.LatencyMs;
            existing.ErrorMessage = request.ErrorMessage;
            existing.Description = request.Description;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Existing incident {IncidentNumber} updated for target {Target}.",
                existing.IncidentNumber,
                existing.Target);

            return existing;
        }

        var now = DateTimeOffset.UtcNow;

        var incident = new Incident
        {
            Id = Guid.NewGuid(),

            IncidentNumber =
                $"INC-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",

            Title = request.Title,
            Description = request.Description,

            Source = request.Source,
            SourceType = request.SourceType,
            SourceId = request.SourceId,

            Target = request.Target,
            TargetType = request.TargetType,

            Severity = request.Severity,
            Status = IncidentStatus.Open,
            FailureType = request.FailureType,

            HttpStatusCode = request.HttpStatusCode,
            LatencyMs = request.LatencyMs,
            ErrorMessage = request.ErrorMessage,

            DetectedAt = request.DetectedAt,

            CreatedAt = now,
            UpdatedAt = now,

            RequiresApproval = false,
            RemediationAttempted = false,
            RemediationSuccessful = false
        };

        dbContext.Incidents.Add(incident);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Incident {IncidentNumber} created. Target: {Target}, FailureType: {FailureType}.",
            incident.IncidentNumber,
            incident.Target,
            incident.FailureType);

        return incident;
    }

    public async Task<Incident?> ResolveAsync(
        Guid id,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        var incident =
            await dbContext.Incidents
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (incident is null)
            return null;

        incident.Status = IncidentStatus.Resolved;
        incident.Resolution = resolution;
        incident.ResolvedAt = DateTimeOffset.UtcNow;
        incident.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Incident {IncidentNumber} resolved.",
            incident.IncidentNumber);

        return incident;
    }
}

using backend.Agents.Abstractions;
using backend.Agents.Models;
using backend.Agents.Remediation.Services;
using backend.Data;
using backend.Models;
using backend.Realtime;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class IncidentWorkflowService
{
    private readonly MonitoringDbContext _dbContext;
    private readonly IncidentService _incidentService;
    private readonly EndpointService _endpointService;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly RemediationRecommendationService _recommendations;
    private readonly RemediationService _remediationService;
    private readonly WebhookNotificationService _notifications;
    private readonly IConfiguration _configuration;
    private readonly WorkflowNotificationService _channelNotifications;
    private readonly IAiOpsEventPublisher _events;
    private readonly ILogger<IncidentWorkflowService> _logger;

    public IncidentWorkflowService(
        MonitoringDbContext dbContext,
        IncidentService incidentService,
        EndpointService endpointService,
        IAgentOrchestrator orchestrator,
        RemediationRecommendationService recommendations,
        RemediationService remediationService,
        WebhookNotificationService notifications,
        WorkflowNotificationService channelNotifications,
        IConfiguration configuration,
        IAiOpsEventPublisher events,
        ILogger<IncidentWorkflowService> logger)
    {
        _dbContext = dbContext;
        _incidentService = incidentService;
        _endpointService = endpointService;
        _orchestrator = orchestrator;
        _recommendations = recommendations;
        _remediationService = remediationService;
        _notifications = notifications;
        _channelNotifications = channelNotifications;
        _configuration = configuration;
        _events = events;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 500);
        return await _dbContext.WorkflowExecutions
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowExecution?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkflowExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<WorkflowExecution?> ExecuteForAnomalyAsync(
        Guid anomalyId,
        CancellationToken cancellationToken = default)
    {
        var anomaly = await _dbContext.DetectedAnomalies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == anomalyId, cancellationToken);
        if (anomaly is null)
        {
            return null;
        }

        var execution = await _dbContext.WorkflowExecutions
            .FirstOrDefaultAsync(x => x.AnomalyId == anomalyId, cancellationToken);
        if (execution is null)
        {
            execution = new WorkflowExecution
            {
                AnomalyId = anomalyId
            };
            _dbContext.WorkflowExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await PublishWorkflowEventAsync(execution);
        }
        else if (execution.Status == "Completed")
        {
            return execution;
        }
        else if (execution.Status == "Running" &&
                 execution.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            return execution;
        }

        if (execution.Status == "Running")
        {
            FailActiveStage(execution, "Previous workflow attempt stopped responding.");
        }

        execution.Status = "Running";
        execution.CurrentStage = null;
        execution.Error = null;
        execution.AttemptCount++;
        execution.StartedAt = DateTimeOffset.UtcNow;
        execution.CompletedAt = null;
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await StartStageAsync(execution, "Create or update incident", cancellationToken);
            var endpoint = _endpointService.GetById(anomaly.EndpointId);
            var incident = await _incidentService.CreateOrUpdateAsync(
                new CreateIncidentRequest(
                    Title: $"{anomaly.Severity} anomaly detected for {endpoint?.Url ?? anomaly.EndpointId.ToString()}",
                    Description: anomaly.Description,
                    Source: "AnomalyDetection",
                    SourceType: "Anomaly",
                    SourceId: anomaly.Id,
                    Target: endpoint?.Url ?? anomaly.EndpointId.ToString(),
                    TargetType: "Endpoint",
                    Severity: ParseSeverity(anomaly.Severity),
                    FailureType: FailureType.Anomaly,
                    HttpStatusCode: null,
                    LatencyMs: null,
                    ErrorMessage: anomaly.Description,
                    DetectedAt: anomaly.DetectedAt),
                cancellationToken);

            execution.IncidentId = incident.Id;
            await CompleteStageAsync(execution, "Incident created or updated.", cancellationToken);
            await StartStageAsync(execution, "RCA and knowledge search", cancellationToken);

            var result = await _orchestrator.ExecuteInvestigationAsync(
                new AgentContext
                {
                    IncidentId = incident.Id.ToString(),
                    TargetType = "Incident",
                    ExecutionId = execution.Id
                },
                cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            await CompleteStageAsync(execution, "RCA and knowledge search completed.", cancellationToken);
            await StartStageAsync(execution, "Create guarded remediation recommendation", cancellationToken);

            var refreshedIncident = await _incidentService.GetByIdAsync(
                incident.Id,
                cancellationToken) ?? incident;
            var recommendation = _recommendations.Recommend(refreshedIncident);
            var remediation = execution.RemediationId is Guid priorRemediationId
                ? await _remediationService.GetByIdAsync(priorRemediationId, cancellationToken)
                : null;
            remediation ??= await _remediationService.CreateAsync(
                    new CreateRemediationRequest
                    {
                        IncidentId = incident.Id,
                        ActionType = recommendation.ActionType,
                        RunbookName = recommendation.RunbookName,
                        Reason = recommendation.Reason,
                        RequestedBy = "IncidentWorkflow"
                    },
                    cancellationToken);

            if (remediation is null)
            {
                throw new InvalidOperationException(
                    $"Unable to create a remediation recommendation for incident {incident.IncidentNumber}.");
            }

            execution.RemediationId = remediation.Id;
            await PublishRemediationEventAsync(remediation);
            await CompleteStageAsync(execution, "Guarded remediation recommendation created.", cancellationToken);
            await StartStageAsync(execution, "Send email, SMS, and webhook notifications", cancellationToken);

            string? notificationWarning = null;
            var delivery = await _channelNotifications.NotifyAsync(
                execution,
                refreshedIncident,
                remediation,
                cancellationToken);
            execution.EmailNotificationStatus = delivery.EmailStatus;
            execution.SmsNotificationStatus = delivery.SmsStatus;
            execution.NotificationError = delivery.Error;
            execution.WebhookNotificationStatus =
                string.IsNullOrWhiteSpace(_configuration["Notifications:WebhookUrl"])
                    ? "NotConfigured"
                    : "Failed";
            if (execution.WebhookNotificationStatus != "NotConfigured")
            {
                try
                {
                    await _notifications.NotifyWorkflowCompletedAsync(
                        execution,
                        refreshedIncident,
                        remediation,
                        cancellationToken);
                    execution.WebhookNotificationStatus = "Sent";
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    notificationWarning = "Webhook notification failed; see backend logs.";
                    execution.NotificationError = string.Join(
                        " ",
                        new[] { execution.NotificationError, notificationWarning }.Where(message => !string.IsNullOrWhiteSpace(message)));
                    _logger.LogError(
                        exception,
                        "Notification delivery failed for incident workflow {WorkflowId}.",
                        execution.Id);
                }
            }
            notificationWarning ??= delivery.Error;

            await CompleteStageAsync(
                execution,
                notificationWarning ?? "Configured notification channels delivered successfully.",
                cancellationToken);
            execution.Status = "Completed";
            execution.CurrentStage = null;
            execution.Error = notificationWarning is null
                ? null
                : $"Workflow completed with notification delivery warnings: {notificationWarning}";
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.UpdatedAt = execution.CompletedAt.Value;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await PublishWorkflowEventAsync(execution);

            _logger.LogInformation(
                "Incident workflow {WorkflowId} completed for anomaly {AnomalyId} and incident {IncidentId}.",
                execution.Id,
                anomaly.Id,
                incident.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FailActiveStage(execution, "Workflow was cancelled before this stage completed.");
            execution.Status = "Failed";
            execution.CurrentStage = null;
            execution.Error = "Workflow was cancelled before completion.";
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.UpdatedAt = execution.CompletedAt.Value;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await PublishWorkflowEventAsync(execution);
            throw;
        }
        catch (OperationCanceledException)
        {
            FailActiveStage(execution, "Workflow execution was interrupted.");
            execution.Status = "Failed";
            execution.CurrentStage = null;
            execution.Error = "Workflow execution was interrupted.";
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.UpdatedAt = execution.CompletedAt.Value;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await PublishWorkflowEventAsync(execution);
            throw;
        }
        catch (Exception exception)
        {
            FailActiveStage(execution, exception.Message);
            execution.Status = "Failed";
            execution.CurrentStage = null;
            execution.Error = exception.Message;
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.UpdatedAt = execution.CompletedAt.Value;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await _events.WorkflowUpdatedAsync(execution, CancellationToken.None);
            _logger.LogError(
                exception,
                "Incident workflow {WorkflowId} failed while processing anomaly {AnomalyId}.",
                execution.Id,
                anomaly.Id);
        }

        return execution;
    }

    private async Task StartStageAsync(
        WorkflowExecution workflow,
        string name,
        CancellationToken cancellationToken)
    {
        var stages = workflow.StageHistory.ToList();
        stages.Add(new WorkflowStageEvent
        {
            Name = name,
            Status = "Running",
            StartedAt = DateTimeOffset.UtcNow
        });
        workflow.StageHistory = stages;
        workflow.CurrentStage = name;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishWorkflowEventAsync(workflow);
    }

    private async Task CompleteStageAsync(
        WorkflowExecution workflow,
        string details,
        CancellationToken cancellationToken)
    {
        var stages = workflow.StageHistory.ToList();
        var activeIndex = stages.FindLastIndex(stage => stage.Status == "Running");
        if (activeIndex >= 0)
        {
            var active = stages[activeIndex];
            stages[activeIndex] = new WorkflowStageEvent
            {
                Name = active.Name,
                Status = "Completed",
                StartedAt = active.StartedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Details = details
            };
        }

        workflow.StageHistory = stages;
        workflow.CurrentStage = null;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishWorkflowEventAsync(workflow);
    }

    private static void FailActiveStage(WorkflowExecution workflow, string details)
    {
        var stages = workflow.StageHistory.ToList();
        var activeIndex = stages.FindLastIndex(stage => stage.Status == "Running");
        if (activeIndex >= 0)
        {
            var active = stages[activeIndex];
            stages[activeIndex] = new WorkflowStageEvent
            {
                Name = active.Name,
                Status = "Failed",
                StartedAt = active.StartedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Details = details
            };
            workflow.StageHistory = stages;
        }

        workflow.CurrentStage = null;
    }

    public async Task<WorkflowExecution?> RetryAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _dbContext.WorkflowExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workflowId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        if (workflow.Status == "Running" &&
            workflow.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            return workflow;
        }

        return await ExecuteForAnomalyAsync(workflow.AnomalyId, cancellationToken);
    }

    private async Task PublishWorkflowEventAsync(WorkflowExecution workflow)
    {
        try
        {
            await _events.WorkflowUpdatedAsync(workflow, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to publish real-time update for workflow {WorkflowId}.",
                workflow.Id);
        }
    }

    private async Task PublishRemediationEventAsync(RemediationAction remediation)
    {
        try
        {
            await _events.RemediationUpdatedAsync(remediation, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to publish real-time update for remediation {RemediationId}.",
                remediation.Id);
        }
    }

    private static IncidentSeverity ParseSeverity(string severity)
    {
        if (Enum.TryParse<IncidentSeverity>(severity, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return severity.ToUpperInvariant() switch
        {
            "CRITICAL" => IncidentSeverity.P1,
            "HIGH" => IncidentSeverity.P2,
            "MEDIUM" => IncidentSeverity.P3,
            _ => IncidentSeverity.P4
        };
    }
}

using backend.Agents.Remediation.Policies;
using backend.Agents.Remediation.Core;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class RemediationService
{
    private readonly MonitoringDbContext _dbContext;
    private readonly IRemediationPolicyEngine _policyEngine;
    private readonly ILogger<RemediationService> _logger;

    public RemediationService(
        MonitoringDbContext dbContext,
        IRemediationPolicyEngine policyEngine,
        ILogger<RemediationService> logger)
    {
        _dbContext = dbContext;
        _policyEngine = policyEngine;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RemediationAction>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemediationActions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RemediationAction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemediationActions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<RemediationAction>> GetByIncidentIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemediationActions
            .AsNoTracking()
            .Where(x => x.IncidentId == incidentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
    public async Task<RemediationAction?> CreateAsync(
        CreateRemediationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Policy is authoritative for risk and approval.
        // The caller/agent does not determine these values.
        var policy =
            _policyEngine.GetPolicy(request.ActionType);

        var incident =
            await _dbContext.Incidents
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == request.IncidentId,
                    cancellationToken);

        if (incident is null)
        {
            return null;
        }

        var isKubernetesAction = request.ActionType is
            RemediationActionType.RestartPod or
            RemediationActionType.RestartDeployment or
            RemediationActionType.ScaleDeployment;
        if (isKubernetesAction &&
            (string.IsNullOrWhiteSpace(request.Target) ||
             !KubernetesOperations.IsValidTarget(request.Target)))
        {
            throw new ArgumentException(
                "Kubernetes remediation requires a target in namespace/resource-name format.",
                nameof(request));
        }

        // Default-deny / safest fallback for an action
        // that has no configured policy.
        var riskLevel =
            policy?.RiskLevel
            ?? RemediationRiskLevel.High;

        var approvalRequired =
            policy?.ApprovalRequired
            ?? true;

        var escalationRequired =
            policy?.EscalationRequired
            ?? true;

        var now =
            DateTimeOffset.UtcNow;

        var remediation =
            new RemediationAction
            {
                Id =
                    Guid.NewGuid(),

                RemediationNumber =
                    GenerateRemediationNumber(),

                IncidentId =
                    incident.Id,

                Target =
                    request.Target?.Trim() ?? incident.Target,

                TargetType =
                    isKubernetesAction ? "Kubernetes" : incident.TargetType,

                ActionType =
                    request.ActionType,

                // Never trust a caller-supplied risk level.
                RiskLevel =
                    riskLevel,

                Status =
                    escalationRequired
                        ? RemediationStatus.Escalated
                        : approvalRequired
                            ? RemediationStatus.AwaitingApproval
                            : RemediationStatus.Proposed,

                ApprovalStatus =
                    approvalRequired
                        ? RemediationApprovalStatus.Pending
                        : RemediationApprovalStatus.NotRequired,

                RunbookName =
                    request.RunbookName?.Trim(),

                Reason =
                    request.Reason?.Trim(),

                RequestedBy =
                    request.RequestedBy?.Trim(),

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        _dbContext.RemediationActions.Add(
            remediation);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Remediation {RemediationNumber} created for incident {IncidentNumber}. " +
            "Action: {ActionType}, Risk: {RiskLevel}, Status: {Status}, Approval: {ApprovalStatus}.",
            remediation.RemediationNumber,
            incident.IncidentNumber,
            remediation.ActionType,
            remediation.RiskLevel,
            remediation.Status,
            remediation.ApprovalStatus);

        return remediation;
    }
    public async Task<RemediationAction?> UpdateAsync(
        Guid id,
        UpdateRemediationRequest request,
        CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
        {
            return null;
        }

        // if (request.Status.HasValue)
        // {
        //     remediation.Status =
        //         request.Status.Value;
        // }

        // if (request.ApprovalStatus.HasValue)
        // {
        //     remediation.ApprovalStatus =
        //         request.ApprovalStatus.Value;
        // }

        // if (request.ApprovedBy is not null)
        // {
        //     remediation.ApprovedBy =
        //         request.ApprovedBy.Trim();
        // }


        if (request.ExecutionOutput is not null)
        {
            remediation.ExecutionOutput =
                request.ExecutionOutput.Trim();
        }

        if (request.FailureReason is not null)
        {
            remediation.FailureReason =
                request.FailureReason.Trim();
        }

        if (request.VerificationSuccessful.HasValue)
        {
            remediation.VerificationSuccessful =
                request.VerificationSuccessful.Value;

            remediation.VerifiedAt =
                DateTimeOffset.UtcNow;
        }

        remediation.UpdatedAt =
            DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }

    private static string GenerateRemediationNumber()
    {
        return
            $"REM-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    public async Task<RemediationAction?> GetActiveAsync(
    Guid incidentId,
    RemediationActionType actionType,
    CancellationToken cancellationToken = default)
    {
        return await _dbContext.RemediationActions
            .AsNoTracking()
            .Where(x =>
                x.IncidentId == incidentId &&
                x.ActionType == actionType &&
                x.Status != RemediationStatus.Succeeded &&
                x.Status != RemediationStatus.Failed &&
                x.Status != RemediationStatus.Rejected)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RemediationAction?> ApproveAsync(
    Guid id,
    string approvedBy,
    CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        remediation.ApprovalStatus =
            RemediationApprovalStatus.Approved;

        remediation.Status =
            RemediationStatus.Approved;

        remediation.ApprovedBy =
            approvedBy;

        remediation.ApprovedAt =
            now;

        remediation.UpdatedAt =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }


    public async Task<RemediationAction?> RejectAsync(
        Guid id,
        string rejectedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        remediation.Status =
            RemediationStatus.Rejected;

        remediation.ApprovalStatus =
            RemediationApprovalStatus.Rejected;

        remediation.RejectedBy =
            rejectedBy;

        remediation.RejectionReason =
            reason?.Trim();

        remediation.UpdatedAt =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }

    public async Task<RemediationAction?> MarkExecutingAsync(
    Guid id,
    CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        remediation.Status =
            RemediationStatus.Executing;

        remediation.StartedAt =
            now;

        remediation.UpdatedAt =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }

    public async Task<RemediationAction?> MarkExecutionCompletedAsync(
    Guid id,
    string? output,
    CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        /*
         * Execution succeeded, but remediation has NOT
         * yet been verified.
         */
        remediation.Status =
            RemediationStatus.Verifying;

        remediation.ExecutionOutput =
            output;

        remediation.FailureReason =
            null;

        remediation.CompletedAt =
            now;

        remediation.UpdatedAt =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }

    public async Task<RemediationAction?> MarkExecutionFailedAsync(
    Guid id,
    string failureReason,
    CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        remediation.Status =
            RemediationStatus.Failed;

        remediation.FailureReason =
            failureReason;

        remediation.CompletedAt =
            now;

        remediation.UpdatedAt =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }

    public async Task<RemediationAction?> MarkVerificationSucceededAsync(
    Guid id,
    string? verificationEvidence,
    CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        remediation.Status =
            RemediationStatus.Succeeded;

        remediation.VerificationSuccessful =
            true;

        remediation.VerifiedAt =
            now;

        remediation.UpdatedAt =
            now;

        if (!string.IsNullOrWhiteSpace(
                verificationEvidence))
        {
            remediation.ExecutionOutput =
                string.IsNullOrWhiteSpace(
                    remediation.ExecutionOutput)
                    ? verificationEvidence
                    : $"{remediation.ExecutionOutput}{Environment.NewLine}Verification: {verificationEvidence}";
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }

    public async Task<RemediationAction?> MarkVerificationFailedAsync(
        Guid id,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var remediation =
            await _dbContext.RemediationActions
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (remediation is null)
            return null;

        var now =
            DateTimeOffset.UtcNow;

        remediation.Status =
            RemediationStatus.Failed;

        remediation.VerificationSuccessful =
            false;

        remediation.FailureReason =
            failureReason;

        remediation.VerifiedAt =
            now;

        remediation.UpdatedAt =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return remediation;
    }
}
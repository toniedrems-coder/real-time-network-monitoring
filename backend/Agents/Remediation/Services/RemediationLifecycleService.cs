using backend.Agents.Remediation.Core;
using backend.Agents.Remediation.Models;
using backend.Agents.Remediation.Policies;
using backend.Models;
using backend.Services;

namespace backend.Agents.Remediation.Services;

public sealed class RemediationLifecycleService
{
    private readonly RemediationService _remediationService;
    private readonly RemediationRunbookExecutor _executor;
    private readonly IRemediationPolicyEngine _policyEngine;
    private readonly ILogger<RemediationLifecycleService> _logger;

    public RemediationLifecycleService(
        RemediationService remediationService,
        RemediationRunbookExecutor executor,
        IRemediationPolicyEngine policyEngine,
        ILogger<RemediationLifecycleService> logger)
    {
        _remediationService = remediationService;
        _executor = executor;
        _policyEngine = policyEngine;
        _logger = logger;
    }

    public async Task<RemediationOperationResult> ApproveAsync(
        Guid remediationId,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            return Failed(
                "ApprovedBy is required.");
        }

        var remediation =
            await _remediationService.GetByIdAsync(
                remediationId,
                cancellationToken);

        if (remediation is null)
        {
            return Failed(
                $"Remediation {remediationId} was not found.");
        }

        if (remediation.Status ==
            RemediationStatus.Rejected)
        {
            return Failed(
                "A rejected remediation cannot be approved.",
                remediation);
        }

        if (remediation.Status is
            RemediationStatus.Executing or
            RemediationStatus.Verifying or
            RemediationStatus.Succeeded or
            RemediationStatus.Failed)
        {
            return Failed(
                $"Remediation cannot be approved while in status {remediation.Status}.",
                remediation);
        }

        var policy =
            _policyEngine.Evaluate(remediation);

        /*
         * Low-risk actions do not need approval.
         */
        if (!policy.ApprovalRequired)
        {
            return Failed(
                "This remediation does not require approval.",
                remediation,
                policy);
        }

        /*
         * High-risk remediation remains an escalation.
         *
         * Approval alone must not convert an escalated action
         * into an automatically executable action.
         */
        if (policy.EscalationRequired)
        {
            return Failed(
                "This remediation requires manual escalation and cannot be approved for automatic execution through this workflow.",
                remediation,
                policy);
        }

        if (remediation.ApprovalStatus ==
            RemediationApprovalStatus.Approved)
        {
            return new RemediationOperationResult
            {
                Success = true,
                Message =
                    "Remediation is already approved.",
                Remediation = remediation,
                PolicyDecision = policy
            };
        }

        remediation =
            await _remediationService.ApproveAsync(
                remediation.Id,
                approvedBy.Trim(),
                cancellationToken);

        return new RemediationOperationResult
        {
            Success = true,
            Message =
                "Remediation approved successfully.",
            Remediation = remediation,
            PolicyDecision =
                remediation is null
                    ? policy
                    : _policyEngine.Evaluate(remediation)
        };
    }

    public async Task<RemediationOperationResult> RejectAsync(
        Guid remediationId,
        string rejectedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rejectedBy))
        {
            return Failed(
                "RejectedBy is required.");
        }

        var remediation =
            await _remediationService.GetByIdAsync(
                remediationId,
                cancellationToken);

        if (remediation is null)
        {
            return Failed(
                $"Remediation {remediationId} was not found.");
        }

        if (remediation.Status is
            RemediationStatus.Executing or
            RemediationStatus.Verifying or
            RemediationStatus.Succeeded or
            RemediationStatus.Failed)
        {
            return Failed(
                $"Remediation cannot be rejected while in status {remediation.Status}.",
                remediation);
        }

        if (remediation.Status ==
            RemediationStatus.Rejected)
        {
            return new RemediationOperationResult
            {
                Success = true,
                Message =
                    "Remediation is already rejected.",
                Remediation = remediation
            };
        }

        remediation =
            await _remediationService.RejectAsync(
                remediation.Id,
                rejectedBy.Trim(),
                reason,
                cancellationToken);

        return new RemediationOperationResult
        {
            Success = true,
            Message =
                "Remediation rejected successfully.",
            Remediation = remediation
        };
    }

    public async Task<RemediationOperationResult> ExecuteAsync(
        Guid remediationId,
        CancellationToken cancellationToken = default)
    {
        var remediation =
            await _remediationService.GetByIdAsync(
                remediationId,
                cancellationToken);

        if (remediation is null)
        {
            return Failed(
                $"Remediation {remediationId} was not found.");
        }

        if (remediation.Status ==
            RemediationStatus.Rejected)
        {
            return Failed(
                "Rejected remediation cannot be executed.",
                remediation);
        }

        if (remediation.Status is
            RemediationStatus.Executing or
            RemediationStatus.Verifying or
            RemediationStatus.Succeeded)
        {
            return Failed(
                $"Remediation cannot execute while in status {remediation.Status}.",
                remediation);
        }

        /*
         * Critical:
         * Re-evaluate immediately before execution.
         */
        var policy =
            _policyEngine.Evaluate(remediation);

        if (!policy.ExecutionAllowed)
        {
            return Failed(
                policy.Reason,
                remediation,
                policy);
        }

        remediation =
            await _remediationService.MarkExecutingAsync(
                remediation.Id,
                cancellationToken);

        if (remediation is null)
        {
            return Failed(
                "Unable to transition remediation to Executing.");
        }

        RemediationExecutionResult execution;

        try
        {
            execution =
                await _executor.ExecuteAsync(
                    remediation.Id,
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Remediation {RemediationId} execution failed.",
                remediation.Id);

            await _remediationService.MarkExecutionFailedAsync(
                remediation.Id,
                exception.Message,
                cancellationToken);

            return Failed(
                "Remediation execution failed.",
                remediation,
                policy);
        }

        if (!execution.Success)
        {
            remediation =
                await _remediationService.MarkExecutionFailedAsync(
                    remediation.Id,
                    execution.FailureReason
                        ?? execution.Message,
                    cancellationToken);

            return new RemediationOperationResult
            {
                Success = false,
                Message =
                    "Remediation runbook execution failed.",
                Remediation = remediation,
                Execution = execution,
                PolicyDecision = policy
            };
        }

        remediation =
            await _remediationService.MarkExecutionCompletedAsync(
                remediation.Id,
                execution.Output
                    ?? execution.Message,
                cancellationToken);

        return new RemediationOperationResult
        {
            Success = true,
            Message =
                "Remediation runbook executed successfully. Verification is required.",
            Remediation = remediation,
            Execution = execution,
            PolicyDecision = policy
        };
    }

    private static RemediationOperationResult Failed(
        string message,
        RemediationAction? remediation = null,
        RemediationPolicyDecision? policy = null)
    {
        return new RemediationOperationResult
        {
            Success = false,
            Message = message,
            Remediation = remediation,
            PolicyDecision = policy
        };
    }



}
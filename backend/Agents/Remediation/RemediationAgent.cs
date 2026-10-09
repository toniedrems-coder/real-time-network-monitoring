using backend.Agents.Abstractions;
using backend.Agents.Models;
using backend.Agents.Remediation.Core;
using backend.Agents.Remediation.Models;
using backend.Agents.Remediation.Policies;
using backend.Agents.Remediation.Services;
using backend.Models;
using backend.Services;

namespace backend.Agents.Remediation;

public sealed class RemediationAgent : IAiOpsAgent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RemediationAgent> _logger;

    public RemediationAgent(
        IServiceScopeFactory scopeFactory,
        ILogger<RemediationAgent> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Id =>
        "remediation-agent";

    public string Name =>
        "Remediation Agent";

    public string Description =>
        "Analyzes incidents and proposes guarded strongly typed remediation actions.";

    public AgentStatus Status { get; private set; } =
        AgentStatus.Stopped;

    public bool Enabled =>
        true;

    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt =
            DateTimeOffset.UtcNow;

        Status =
            AgentStatus.Investigating;

        try
        {
            if (string.IsNullOrWhiteSpace(
                    context.IncidentId))
            {
                Status =
                    AgentStatus.Failed;

                return AgentResult.Failed(
                    Name,
                    "IncidentId is required for remediation.",
                    startedAt);
            }

            if (!Guid.TryParse(
                    context.IncidentId,
                    out var incidentId))
            {
                Status =
                    AgentStatus.Failed;

                return AgentResult.Failed(
                    Name,
                    $"Invalid IncidentId '{context.IncidentId}'.",
                    startedAt);
            }

            using var scope =
                _scopeFactory.CreateScope();

            var incidentService =
                scope.ServiceProvider
                    .GetRequiredService<IncidentService>();

            var remediationService =
                scope.ServiceProvider
                    .GetRequiredService<RemediationService>();

            var recommendationService =
                scope.ServiceProvider
                    .GetRequiredService<
                        RemediationRecommendationService>();

            var policyEngine =
                scope.ServiceProvider
                    .GetRequiredService<
                        IRemediationPolicyEngine>();

            var runbookRegistry =
                scope.ServiceProvider
                    .GetRequiredService<
                        RemediationRunbookRegistry>();

            var incident =
                await incidentService.GetByIdAsync(
                    incidentId,
                    cancellationToken);

            if (incident is null)
            {
                Status =
                    AgentStatus.Failed;

                return AgentResult.Failed(
                    Name,
                    $"Incident {incidentId} was not found.",
                    startedAt);
            }

            var recommendation =
                recommendationService.Recommend(
                    incident);

            var existingRemediation =
    await remediationService.GetActiveAsync(
        incident.Id,
        recommendation.ActionType,
        cancellationToken);

            if (existingRemediation is not null)
            {
                var existingPolicyDecision =
                    policyEngine.Evaluate(
                        existingRemediation);

                Status =
                    AgentStatus.Running;

                return AgentResult.Successful(
                    Name,
                    $"An active remediation already exists for incident {incident.IncidentNumber}.",
                    startedAt,
                    new Dictionary<string, object>
                    {
                        ["incidentId"] =
                            incident.Id,

                        ["incidentNumber"] =
                            incident.IncidentNumber,

                        ["remediationId"] =
                            existingRemediation.Id,

                        ["remediationNumber"] =
                            existingRemediation.RemediationNumber,

                        ["actionType"] =
                            existingRemediation.ActionType.ToString(),

                        ["riskLevel"] =
                            existingPolicyDecision.RiskLevel.ToString(),

                        ["executionAllowed"] =
                            existingPolicyDecision.ExecutionAllowed,

                        ["approvalRequired"] =
                            existingPolicyDecision.ApprovalRequired,

                        ["escalationRequired"] =
                            existingPolicyDecision.EscalationRequired,

                        ["duplicatePrevented"] =
                            true
                    });
            }

            /*
             * Important safety check:
             * The agent may only recommend a runbook that is
             * registered with the strongly typed framework.
             *
             * ExecuteRunbook/manual fallback is allowed to remain
             * a proposal requiring escalation, but it is never
             * automatically executed here.
             */
            var registeredRunbook =
                runbookRegistry.GetById(
                    recommendation.RunbookName);

            if (registeredRunbook is null &&
                recommendation.ActionType !=
                    RemediationActionType.ExecuteRunbook)
            {
                Status =
                    AgentStatus.Failed;

                return AgentResult.Failed(
                    Name,
                    $"Runbook '{recommendation.RunbookName}' is not registered.",
                    startedAt);
            }

            Status =
                AgentStatus.Executing;

            var createRequest =
                new CreateRemediationRequest
                {
                    IncidentId =
                        incident.Id,

                    ActionType =
                        recommendation.ActionType,

                    RunbookName =
                        recommendation.RunbookName,

                    Reason =
                        recommendation.Reason,

                    RequestedBy =
                        Name
                };

            var remediation =
                await remediationService.CreateAsync(
                    createRequest,
                    cancellationToken);

            if (remediation is null)
            {
                Status =
                    AgentStatus.Failed;

                return AgentResult.Failed(
                    Name,
                    "Unable to create remediation action.",
                    startedAt);
            }

            /*
             * Policy evaluates the persisted remediation.
             * Risk remains authoritative from policy, not agent.
             */
            var policyDecision =
                policyEngine.Evaluate(
                    remediation);

            Status =
                AgentStatus.Running;

            var data =
                new Dictionary<string, object>
                {
                    ["incidentId"] =
                        incident.Id,

                    ["incidentNumber"] =
                        incident.IncidentNumber,

                    ["remediationId"] =
                        remediation.Id,

                    ["remediationNumber"] =
                        remediation.RemediationNumber,

                    ["target"] =
                        remediation.Target,

                    ["actionType"] =
                        remediation.ActionType.ToString(),

                    ["runbookName"] =
                        remediation.RunbookName
                        ?? string.Empty,

                    ["recommendationReason"] =
                        recommendation.Reason,

                    ["recommendationConfidence"] =
                        recommendation.Confidence,

                    ["riskLevel"] =
                        policyDecision.RiskLevel.ToString(),

                    ["executionAllowed"] =
                        policyDecision.ExecutionAllowed,

                    ["approvalRequired"] =
                        policyDecision.ApprovalRequired,

                    ["escalationRequired"] =
                        policyDecision.EscalationRequired,

                    ["policyName"] =
                        policyDecision.PolicyName
                        ?? string.Empty,

                    ["remediationStatus"] =
                        remediation.Status.ToString(),

                    ["approvalStatus"] =
                        remediation.ApprovalStatus.ToString()
                };

            _logger.LogInformation(
                "Remediation Agent proposed {ActionType} for incident {IncidentNumber}. Risk: {RiskLevel}, ApprovalRequired: {ApprovalRequired}.",
                remediation.ActionType,
                incident.IncidentNumber,
                policyDecision.RiskLevel,
                policyDecision.ApprovalRequired);

            return AgentResult.Successful(
                Name,
                $"Remediation proposed for incident {incident.IncidentNumber}.",
                startedAt,
                data);
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
                "Remediation Agent execution failed.");

            return AgentResult.Failed(
                Name,
                exception.Message,
                startedAt);
        }
    }
}
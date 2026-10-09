using backend.Agents.Knowledge.Models;
using backend.Agents.Remediation.Models;
using backend.Models;
using backend.Services;

namespace backend.Agents.Remediation.Services;

public sealed class RemediationVerificationWorkflowService
{
    private readonly RemediationVerificationService
        _verificationService;

    private readonly RemediationService
        _remediationService;

    private readonly IncidentService
        _incidentService;

    private readonly IncidentKnowledgeMatchService
        _knowledgeMatchService;

    private readonly IncidentLearningService
        _learningService;

    private readonly ILogger<RemediationVerificationWorkflowService>
        _logger;

    public RemediationVerificationWorkflowService(
        RemediationVerificationService verificationService,
        RemediationService remediationService,
        IncidentService incidentService,
        IncidentKnowledgeMatchService knowledgeMatchService,
        IncidentLearningService learningService,
        ILogger<RemediationVerificationWorkflowService> logger)
    {
        _verificationService =
            verificationService;

        _remediationService =
            remediationService;

        _incidentService =
            incidentService;

        _knowledgeMatchService =
            knowledgeMatchService;

        _learningService =
            learningService;

        _logger =
            logger;
    }

    public async Task<RemediationVerificationWorkflowResult>
        VerifyAsync(
            Guid remediationId,
            CancellationToken cancellationToken = default)
    {
        var verification =
            await _verificationService.VerifyAsync(
                remediationId,
                cancellationToken);

        var remediation =
            await _remediationService.GetByIdAsync(
                remediationId,
                cancellationToken);

        if (remediation is null)
        {
            return new RemediationVerificationWorkflowResult
            {
                Success = false,
                Message = "Remediation was not found.",
                Verification = verification
            };
        }

        /*
         * Verification failed.
         *
         * Do NOT resolve the incident.
         * Record failed knowledge outcome when a selected
         * knowledge article exists.
         */
        if (!verification.Successful)
        {
            remediation =
                await _remediationService
                    .MarkVerificationFailedAsync(
                        remediation.Id,
                        verification.Message,
                        cancellationToken);

            await RecordKnowledgeOutcomeAsync(
                remediation!.IncidentId,
                successful: false,
                cancellationToken);

            var incident =
                await _incidentService.GetByIdAsync(
                    remediation.IncidentId,
                    cancellationToken);

            return new RemediationVerificationWorkflowResult
            {
                Success = false,

                Message =
                    "Remediation verification failed. Incident remains unresolved.",

                Remediation =
                    remediation,

                Verification =
                    verification,

                Incident =
                    incident
            };
        }

        /*
         * Recovery has been proven.
         */
        remediation =
            await _remediationService
                .MarkVerificationSucceededAsync(
                    remediation.Id,
                    verification.Evidence,
                    cancellationToken);

        await RecordKnowledgeOutcomeAsync(
            remediation!.IncidentId,
            successful: true,
            cancellationToken);

        /*
         * Resolve incident only AFTER verification succeeds.
         */
        var resolution =
            $"Remediation {remediation.RemediationNumber} succeeded and verification confirmed recovery. {verification.Message}";

        var resolvedIncident =
            await _incidentService.ResolveAsync(
                remediation.IncidentId,
                resolution,
                cancellationToken);

        IncidentLearningResult? learningResult =
            null;

        if (resolvedIncident is not null)
        {
            try
            {
                learningResult =
                    await _learningService.LearnFromResolvedIncidentAsync(
                        resolvedIncident.Id,
                        cancellationToken);
            }
            catch (Exception exception)
            {
                /*
                 * Learning failure must not undo a verified
                 * remediation or resolved incident.
                 */
                _logger.LogError(
                    exception,
                    "Incident learning failed after successful remediation {RemediationNumber}.",
                    remediation.RemediationNumber);
            }
        }

        return new RemediationVerificationWorkflowResult
        {
            Success = true,

            Message =
                "Remediation verified successfully and incident resolved.",

            Remediation =
                remediation,

            Verification =
                verification,

            Incident =
                resolvedIncident,

            LearningResult =
                learningResult
        };
    }

    private async Task RecordKnowledgeOutcomeAsync(
        Guid incidentId,
        bool successful,
        CancellationToken cancellationToken)
    {
        var selectedMatch =
            await _knowledgeMatchService
                .GetSelectedMatchAsync(
                    incidentId,
                    cancellationToken);

        if (selectedMatch is null)
            return;

        await _knowledgeMatchService
            .MarkSelectedMatchOutcomeAsync(
                incidentId,
                successful,
                cancellationToken);
    }
}
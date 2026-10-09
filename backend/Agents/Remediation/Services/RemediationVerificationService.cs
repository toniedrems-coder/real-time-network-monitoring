using backend.Agents.Remediation.Models;
using backend.Agents.Tools.Monitoring;
using backend.Models;
using backend.Services;

namespace backend.Agents.Remediation.Services;

public sealed class RemediationVerificationService
{
    private readonly RemediationService _remediationService;
    private readonly EndpointService _endpointService;
    private readonly IEndpointProbeService _probeService;
    private readonly ILogger<RemediationVerificationService> _logger;

    public RemediationVerificationService(
        RemediationService remediationService,
        EndpointService endpointService,
        IEndpointProbeService probeService,
        ILogger<RemediationVerificationService> logger)
    {
        _remediationService = remediationService;
        _endpointService = endpointService;
        _probeService = probeService;
        _logger = logger;
    }

    public async Task<RemediationVerificationResult> VerifyAsync(
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
                remediationId,
                Guid.Empty,
                "Remediation was not found.");
        }

        if (remediation.Status !=
            RemediationStatus.Verifying)
        {
            return Failed(
                remediation.Id,
                remediation.IncidentId,
                $"Remediation cannot be verified while in status {remediation.Status}.");
        }

        return remediation.ActionType switch
        {
            RemediationActionType.RecheckEndpoint =>
                await VerifyEndpointAsync(
                    remediation,
                    cancellationToken),

            _ =>
                Failed(
                    remediation.Id,
                    remediation.IncidentId,
                    $"No verification strategy is registered for action type {remediation.ActionType}.")
        };
    }

    private async Task<RemediationVerificationResult>
        VerifyEndpointAsync(
            RemediationAction remediation,
            CancellationToken cancellationToken)
    {
        /*
         * IMPORTANT:
         * Replace GetAll()/GetAllAsync below with the exact
         * EndpointService method already used by your
         * EndpointRecheckRunbook.
         *
         * We previously discovered your EndpointService does
         * not expose the guessed GetAllAsync method.
         */

        var endpoints =
            _endpointService.GetAll();

        var endpoint =
            endpoints.FirstOrDefault(
                x => string.Equals(
                    x.Url?.TrimEnd('/'),
                    remediation.Target.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase));

        if (endpoint is null)
        {
            return Failed(
                remediation.Id,
                remediation.IncidentId,
                $"Monitored endpoint '{remediation.Target}' was not found.");
        }

        try
        {
            var probe =
                await _probeService.ProbeAsync(
                    endpoint,
                    cancellationToken);

            var successful =
                probe.Reachable &&
                probe.StatusCode is >= 200 and < 400;

            var message =
                successful
                    ? $"Endpoint {remediation.Target} recovered successfully."
                    : $"Endpoint {remediation.Target} remains unhealthy.";

            _logger.LogInformation(
                "Verification completed for remediation {RemediationNumber}. Successful: {Successful}, Reachable: {Reachable}, StatusCode: {StatusCode}, LatencyMs: {LatencyMs}.",
                remediation.RemediationNumber,
                successful,
                probe.Reachable,
                probe.StatusCode,
                probe.LatencyMs);

            return new RemediationVerificationResult
            {
                RemediationId =
                    remediation.Id,

                IncidentId =
                    remediation.IncidentId,

                Successful =
                    successful,

                Message =
                    message,

                Evidence =
                    $"Reachable={probe.Reachable}; Status={probe.Status}; StatusCode={probe.StatusCode}; LatencyMs={probe.LatencyMs:F2}",

                LatencyMs =
                    probe.LatencyMs,

                StatusCode =
                    probe.StatusCode,

                VerifiedAt =
                    DateTimeOffset.UtcNow
            };
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
                "Verification failed for remediation {RemediationNumber}.",
                remediation.RemediationNumber);

            return Failed(
                remediation.Id,
                remediation.IncidentId,
                exception.Message);
        }
    }

    private static RemediationVerificationResult Failed(
        Guid remediationId,
        Guid incidentId,
        string message)
    {
        return new RemediationVerificationResult
        {
            RemediationId = remediationId,
            IncidentId = incidentId,
            Successful = false,
            Message = message,
            Evidence = message
        };
    }
}
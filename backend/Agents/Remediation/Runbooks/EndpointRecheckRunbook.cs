using backend.Agents.Remediation.Abstractions;
using backend.Agents.Remediation.Models;
using backend.Agents.Tools.Monitoring;
using backend.Models;
using backend.Services;

namespace backend.Agents.Remediation.Runbooks;

public sealed class EndpointRecheckRunbook : IRemediationRunbook
{
    private readonly EndpointService _endpointService;
    private readonly IEndpointProbeService _probeService;
    private readonly ILogger<EndpointRecheckRunbook> _logger;

    public EndpointRecheckRunbook(
        EndpointService endpointService,
        IEndpointProbeService probeService,
        ILogger<EndpointRecheckRunbook> logger)
    {
        _endpointService = endpointService;
        _probeService = probeService;
        _logger = logger;
    }

    public string Id => "endpoint-recheck";

    public string Name => "Endpoint Recheck";

    public RemediationActionType ActionType => RemediationActionType.RecheckEndpoint;

    public RemediationRiskLevel RiskLevel => RemediationRiskLevel.Low;

    public async Task<RemediationExecutionResult> ExecuteAsync(
        RemediationContext context, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Executing runbook {RunbookId} for target {Target}.", Id, context.Target);

        try
        {
            var endpoints = _endpointService.GetAll();


            var endpoint =
                endpoints.FirstOrDefault(
                    x => string.Equals(
                        NormalizeUrl(x.Url),
                        NormalizeUrl(context.Target),
                        StringComparison.OrdinalIgnoreCase));

            if (endpoint is null)
            {
                return new RemediationExecutionResult
                {
                    Success = false,
                    Message =
                        $"Endpoint {context.Target} is not registered for monitoring.",

                    FailureReason =
                        "Monitored endpoint was not found.",

                    StartedAt =
                        startedAt,

                    CompletedAt =
                        DateTimeOffset.UtcNow
                };
            }

            var probeResult =
                await _probeService.ProbeAsync(
                    endpoint,
                    cancellationToken);

            var completedAt =
                DateTimeOffset.UtcNow;

            if (!probeResult.Reachable)
            {
                return new RemediationExecutionResult
                {
                    Success = false,

                    Message =
                        $"Endpoint recheck failed for {context.Target}.",

                    FailureReason =
                        probeResult.ErrorMessage
                        ?? "Endpoint is unreachable.",

                    Output =
                        $"Status={probeResult.Status}; " +
                        $"StatusCode={probeResult.StatusCode}; " +
                        $"LatencyMs={probeResult.LatencyMs}",

                    StartedAt =
                        startedAt,

                    CompletedAt =
                        completedAt,

                    Data = new Dictionary<string, object>
                    {
                        ["reachable"] =
                            probeResult.Reachable,

                        ["status"] =
                            probeResult.Status,

                        ["latencyMs"] = probeResult.LatencyMs
                    }
                };
            }

            return new RemediationExecutionResult
            {
                Success = true,

                Message =
                    $"Endpoint {context.Target} responded successfully.",

                Output =
                    $"Status={probeResult.Status}; " +
                    $"StatusCode={probeResult.StatusCode}; " +
                    $"LatencyMs={probeResult.LatencyMs}",

                StartedAt =
                    startedAt,

                CompletedAt =
                    completedAt,

                Data = new Dictionary<string, object>
                {
                    ["reachable"] =
                        probeResult.Reachable,

                    ["status"] =
                        probeResult.Status,

                    ["statusCode"] =
                        probeResult.StatusCode ?? 0,

                    ["latencyMs"] = probeResult.LatencyMs
                }
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
                "Endpoint recheck runbook failed for {Target}.",
                context.Target);

            return new RemediationExecutionResult
            {
                Success = false,

                Message =
                    $"Endpoint recheck failed for {context.Target}.",

                FailureReason =
                    exception.Message,

                StartedAt =
                    startedAt,

                CompletedAt =
                    DateTimeOffset.UtcNow
            };
        }
    }

    private static string NormalizeUrl(string value)
    {
        return value.Trim().TrimEnd('/');
    }
}
using backend.Agents.Abstractions;
using backend.Agents.Models;
using backend.Agents.Rca.Models;
using backend.Agents.Rca.Storage;
using backend.Agents.Rca.Tools;
using backend.Models;
using backend.Realtime;
using backend.Services;

namespace backend.Agents.Rca;

public sealed class RcaAgent : IAiOpsAgent
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly RootCauseAnalysisService analysisService;
    private readonly ILogger<RcaAgent> logger;
    private readonly IReadOnlyCollection<IInvestigationTool> investigationTools;
    private readonly IRcaResultStore rcaResultStore;
    private readonly IAiOpsEventPublisher eventPublisher;

    public RcaAgent(
        IServiceScopeFactory scopeFactory,
        RootCauseAnalysisService analysisService,
        ILogger<RcaAgent> logger,
        IRcaResultStore rcaResultStore,
        IAiOpsEventPublisher eventPublisher,
        IEnumerable<IInvestigationTool> investigationTools)
    {
        this.scopeFactory = scopeFactory;
        this.analysisService = analysisService;
        this.logger = logger;
        this.rcaResultStore = rcaResultStore;
        this.eventPublisher = eventPublisher;
        this.investigationTools = investigationTools.ToList();
    }

    public string Id => "rca-agent";

    public string Name => "RCA Agent";

    public string Description =>
        "Investigates operational incidents, gathers evidence " +
        "and produces structured root cause analysis.";

    public AgentStatus Status { get; private set; }
        = AgentStatus.Stopped;

    public bool Enabled => true;

    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        Incident? incident = null;

        Status = AgentStatus.Investigating;

        try
        {
            // -------------------------------------------------
            // 1. Validate incident ID
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(context.IncidentId))
            {
                return AgentResult.Failed(
                    Name,
                    "IncidentId is required for RCA.",
                    startedAt);
            }

            if (!Guid.TryParse(
                context.IncidentId,
                out var incidentId))
            {
                return AgentResult.Failed(
                    Name,
                    "IncidentId is invalid.",
                    startedAt);
            }

            // -------------------------------------------------
            // 2. Create scope for IncidentService / DbContext
            // -------------------------------------------------

            using var scope =
                scopeFactory.CreateScope();

            var incidentService =
                scope.ServiceProvider
                    .GetRequiredService<IncidentService>();

            // -------------------------------------------------
            // 3. Load incident
            // -------------------------------------------------

            incident =
                await incidentService.GetByIdAsync(
                    incidentId,
                    cancellationToken);

            if (incident is null)
            {
                return AgentResult.Failed(
                    Name,
                    $"Incident '{incidentId}' was not found.",
                    startedAt);
            }

            logger.LogInformation(
                "RCA Agent investigating incident {IncidentNumber}.",
                incident.IncidentNumber);

            // -------------------------------------------------
            // 4. Publish RCA started event
            // -------------------------------------------------

            await eventPublisher.RcaStartedAsync(
                new RcaEvent
                {
                    IncidentId = incident.Id,
                    IncidentNumber =
                        incident.IncidentNumber,
                    Status = "Investigating",
                    Message =
                        "Root cause analysis started."
                },
                cancellationToken);

            // -------------------------------------------------
            // 5. Mark incident as being investigated
            // -------------------------------------------------

            await incidentService.StartInvestigationAsync(
                incident.Id,
                Name,
                cancellationToken);

            // -------------------------------------------------
            // 6. Collect evidence already stored on incident
            // -------------------------------------------------

            var evidence =
                CollectIncidentEvidence(incident);

            // -------------------------------------------------
            // 7. Determine applicable investigation tools
            // -------------------------------------------------

            var applicableTools =
                investigationTools
                    .Where(tool =>
                        tool.CanInvestigate(incident))
                    .ToList();

            logger.LogInformation(
                "{ToolCount} investigation tools selected " +
                "for incident {IncidentNumber}.",
                applicableTools.Count,
                incident.IncidentNumber);

            // -------------------------------------------------
            // 8. Execute investigation tools
            // -------------------------------------------------

            foreach (var tool in applicableTools)
            {
                try
                {
                    logger.LogInformation(
                        "Executing investigation tool {ToolName} " +
                        "for incident {IncidentNumber}.",
                        tool.Name,
                        incident.IncidentNumber);

                    var toolEvidence =
                        await tool.InvestigateAsync(
                            incident,
                            cancellationToken);

                    evidence.AddRange(toolEvidence);

                    logger.LogInformation(
                        "Investigation tool {ToolName} collected " +
                        "{EvidenceCount} evidence items.",
                        tool.Name,
                        toolEvidence.Count);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Investigation tool {ToolName} failed " +
                        "for incident {IncidentNumber}.",
                        tool.Name,
                        incident.IncidentNumber);

                    // A failed investigation tool should not
                    // terminate the entire RCA investigation.
                    evidence.Add(
                        new RcaEvidence
                        {
                            Source = tool.Name,
                            Type = "ToolFailure",
                            Description =
                                "Investigation tool failed while collecting evidence.",
                            Value =
                                exception.Message
                        });
                }
            }

            // -------------------------------------------------
            // 9. Correlate evidence and perform RCA
            // -------------------------------------------------

            var analysis =
                analysisService.Analyze(
                    incident,
                    evidence);

            // Store the complete RCA result for retrieval
            // through GET /api/rca/{incidentId}.
            rcaResultStore.Save(analysis);

            // -------------------------------------------------
            // 10. Persist RCA summary on incident
            // -------------------------------------------------

            await incidentService.UpdateRcaAsync(
                incident.Id,
                analysis.ProbableCause,
                string.Join(
                    Environment.NewLine,
                    analysis.RecommendedActions),
                cancellationToken);

            logger.LogInformation(
                "RCA completed for incident {IncidentNumber}. " +
                "Confidence: {Confidence}. " +
                "Evidence collected: {EvidenceCount}.",
                incident.IncidentNumber,
                analysis.Confidence,
                evidence.Count);

            // -------------------------------------------------
            // 11. Publish RCA completed event
            // -------------------------------------------------

            await eventPublisher.RcaCompletedAsync(
                new RcaEvent
                {
                    IncidentId = incident.Id,
                    IncidentNumber =
                        incident.IncidentNumber,
                    Status = "Completed",
                    Message =
                        "Root cause analysis completed.",
                    Confidence =
                        analysis.Confidence,
                    ConfidenceLevel =
                        analysis.ConfidenceLevel,
                    RequiresEscalation =
                        analysis.RequiresEscalation
                },
                cancellationToken);

            // -------------------------------------------------
            // 12. Build AgentResult response
            // -------------------------------------------------

            var data =
                new Dictionary<string, object>
                {
                    ["incidentId"] =
                        incident.Id,

                    ["incidentNumber"] =
                        incident.IncidentNumber,

                    ["toolsExecuted"] =
                        applicableTools
                            .Select(tool => tool.Name)
                            .ToList(),

                    ["evidenceCollected"] =
                        evidence.Count,

                    ["analysis"] =
                        analysis
                };

            return AgentResult.Successful(
                Name,
                $"RCA completed for incident {incident.IncidentNumber}.",
                startedAt,
                data);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "RCA Agent execution was cancelled.");

            throw;
        }
        catch (Exception exception)
        {
            Status = AgentStatus.Failed;

            logger.LogError(
                exception,
                "RCA Agent execution failed.");

            // We can only publish an incident-specific failure
            // after an incident has been successfully resolved.
            if (incident is not null)
            {
                try
                {
                    await eventPublisher.RcaFailedAsync(
                        new RcaEvent
                        {
                            IncidentId = incident.Id,
                            IncidentNumber =
                                incident.IncidentNumber,
                            Status = "Failed",
                            Message =
                                exception.Message
                        },
                        CancellationToken.None);
                }
                catch (Exception publishException)
                {
                    // SignalR failure must not hide the
                    // original RCA failure.
                    logger.LogError(
                        publishException,
                        "Failed to publish RcaFailed event " +
                        "for incident {IncidentNumber}.",
                        incident.IncidentNumber);
                }
            }

            return AgentResult.Failed(
                Name,
                exception.Message,
                startedAt);
        }
        finally
        {
            if (Status != AgentStatus.Failed)
            {
                Status = AgentStatus.Stopped;
            }
        }
    }

    private static List<RcaEvidence>
        CollectIncidentEvidence(
            Incident incident)
    {
        var evidence =
            new List<RcaEvidence>();

        evidence.Add(
            new RcaEvidence
            {
                Source = "Incident",
                Type = "FailureType",
                Description =
                    "Failure classification recorded by monitoring.",
                Value =
                    incident.FailureType.ToString()
            });

        if (incident.LatencyMs.HasValue)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = "MonitoringAgent",
                    Type = "Latency",
                    Description =
                        "Last recorded endpoint latency.",
                    Value =
                        $"{incident.LatencyMs.Value:F2} ms"
                });
        }

        if (incident.HttpStatusCode.HasValue)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = "EndpointProbe",
                    Type = "HttpStatusCode",
                    Description =
                        "HTTP status returned by endpoint.",
                    Value =
                        incident.HttpStatusCode.Value.ToString()
                });
        }

        if (!string.IsNullOrWhiteSpace(
            incident.ErrorMessage))
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = "EndpointProbe",
                    Type = "Error",
                    Description =
                        "Error captured during endpoint probe.",
                    Value =
                        incident.ErrorMessage
                });
        }

        evidence.Add(
            new RcaEvidence
            {
                Source = "Incident",
                Type = "Severity",
                Description =
                    "Current incident severity.",
                Value =
                    incident.Severity.ToString()
            });

        return evidence;
    }
}
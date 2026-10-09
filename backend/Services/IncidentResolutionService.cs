using backend.Agents.Knowledge.Models;
using backend.Models;

namespace backend.Services;

public sealed class IncidentResolutionService
{
    private readonly IncidentService _incidentService;

    private readonly IncidentKnowledgeMatchService
        _matchService;

    private readonly IncidentLearningService
        _learningService;

    private readonly ILogger<IncidentResolutionService>
        _logger;

    public IncidentResolutionService(
        IncidentService incidentService,
        IncidentKnowledgeMatchService matchService,
        IncidentLearningService learningService,
        ILogger<IncidentResolutionService> logger)
    {
        _incidentService = incidentService;
        _matchService = matchService;
        _learningService = learningService;
        _logger = logger;
    }

    public async Task<IncidentResolutionResult?>
        ResolveAsync(
            Guid incidentId,
            ResolveIncidentRequest request,
            CancellationToken cancellationToken = default)
    {
        // ---------------------------------------------
        // 1. Validate request
        // ---------------------------------------------

        if (string.IsNullOrWhiteSpace(
                request.Resolution))
        {
            throw new ArgumentException(
                "Resolution is required.",
                nameof(request));
        }

        // ---------------------------------------------
        // 2. Resolve the incident
        // ---------------------------------------------

        var incident =
            await _incidentService.ResolveAsync(
                incidentId,
                request.Resolution,
                cancellationToken);

        if (incident is null)
        {
            return null;
        }

        // ---------------------------------------------
        // 3. Record knowledge outcome
        // ---------------------------------------------

        if (request.KnowledgeSuccessful.HasValue)
        {
            await _matchService
                .MarkSelectedMatchOutcomeAsync(
                    incident.Id,
                    request.KnowledgeSuccessful.Value,
                    cancellationToken);
        }

        // ---------------------------------------------
        // 4. Automatically learn
        // ---------------------------------------------

        IncidentLearningResult? learningResult = null;

        try
        {
            learningResult =
                await _learningService
                    .LearnFromResolvedIncidentAsync(
                        incident.Id,
                        cancellationToken);

            _logger.LogInformation(
                "Automatic incident learning completed for {IncidentNumber}. Learned: {Learned}.",
                incident.IncidentNumber,
                learningResult.Learned);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            /*
             * Resolution has already succeeded.
             *
             * A learning failure should not cause the
             * API to pretend the incident resolution
             * itself failed.
             */

            _logger.LogError(
                exception,
                "Automatic learning failed for resolved incident {IncidentNumber}.",
                incident.IncidentNumber);
        }

        // ---------------------------------------------
        // 5. Return combined result
        // ---------------------------------------------

        return new IncidentResolutionResult
        {
            Incident = incident,

            LearningResult = learningResult,

            LearningAttempted = true,

            LearningMessage =
                learningResult?.Message ??
                "Incident resolved, but automatic learning did not complete."
        };
    }
}
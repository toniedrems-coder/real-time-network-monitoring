using System.Collections.Concurrent;
using backend.Agents.Rca.Models;

namespace backend.Agents.Rca.Storage;

public sealed class InMemoryRcaResultStore
    : IRcaResultStore
{
    private readonly ConcurrentDictionary<
        Guid,
        RootCauseAnalysis> analyses = new();

    public void Save(
        RootCauseAnalysis analysis)
    {
        analyses.AddOrUpdate(
            analysis.IncidentId,
            analysis,
            (_, _) => analysis);
    }

    public RootCauseAnalysis? GetByIncidentId(
        Guid incidentId)
    {
        return analyses.TryGetValue(
            incidentId,
            out var analysis)
                ? analysis
                : null;
    }
}
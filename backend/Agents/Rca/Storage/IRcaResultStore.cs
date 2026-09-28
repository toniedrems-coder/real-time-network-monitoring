using backend.Agents.Rca.Models;

namespace backend.Agents.Rca.Storage;

public interface IRcaResultStore
{
    void Save(RootCauseAnalysis analysis);

    RootCauseAnalysis? GetByIncidentId(
        Guid incidentId);
}
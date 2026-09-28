using backend.Agents.Rca.Models;
using backend.Models;

namespace backend.Agents.Rca.Tools;

public interface IInvestigationTool
{
    string Name { get; }

    string Description { get; }

    bool CanInvestigate(Incident incident);

    Task<IReadOnlyCollection<RcaEvidence>> InvestigateAsync(
        Incident incident,
        CancellationToken cancellationToken = default);
}
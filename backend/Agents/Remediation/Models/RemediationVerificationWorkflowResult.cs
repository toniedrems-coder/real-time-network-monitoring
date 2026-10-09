using backend.Agents.Knowledge.Models;
using backend.Models;

namespace backend.Agents.Remediation.Models;

public sealed record RemediationVerificationWorkflowResult
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public RemediationAction? Remediation { get; init; }

    public RemediationVerificationResult? Verification { get; init; }

    public Incident? Incident { get; init; }

    public IncidentLearningResult? LearningResult { get; init; }
}
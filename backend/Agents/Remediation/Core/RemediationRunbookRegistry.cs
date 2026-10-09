using backend.Agents.Remediation.Abstractions;
using backend.Models;

namespace backend.Agents.Remediation.Core;

public sealed class RemediationRunbookRegistry
{
    private readonly IReadOnlyDictionary<string, IRemediationRunbook>
        _runbooksById;

    private readonly IReadOnlyDictionary<RemediationActionType, IRemediationRunbook>
        _runbooksByActionType;

    public RemediationRunbookRegistry(
        IEnumerable<IRemediationRunbook> runbooks)
    {
        var registeredRunbooks =
            runbooks.ToList();

        _runbooksById =
            registeredRunbooks.ToDictionary(
                x => x.Id,
                StringComparer.OrdinalIgnoreCase);

        _runbooksByActionType =
            registeredRunbooks.ToDictionary(
                x => x.ActionType);
    }

    public IReadOnlyCollection<IRemediationRunbook> GetAll()
    {
        return _runbooksById.Values.ToList();
    }

    public IRemediationRunbook? GetById(
        string runbookId)
    {
        if (string.IsNullOrWhiteSpace(runbookId))
        {
            return null;
        }

        return _runbooksById.TryGetValue(
            runbookId,
            out var runbook)
                ? runbook
                : null;
    }

    public IRemediationRunbook? GetByActionType(
        RemediationActionType actionType)
    {
        return _runbooksByActionType.TryGetValue(
            actionType,
            out var runbook)
                ? runbook
                : null;
    }
}
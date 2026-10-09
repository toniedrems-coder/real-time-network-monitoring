using backend.Agents.Remediation.Policies;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/remediation-policies")]
public sealed class RemediationPoliciesController
    : ControllerBase
{
    private readonly IRemediationPolicyEngine
        _policyEngine;

    private readonly RemediationService
        _remediationService;

    public RemediationPoliciesController(
        IRemediationPolicyEngine policyEngine,
        RemediationService remediationService)
    {
        _policyEngine =
            policyEngine;

        _remediationService =
            remediationService;
    }

    [HttpGet]
    public IActionResult GetPolicies()
    {
        return Ok(
            _policyEngine.GetPolicies());
    }

    [HttpGet("action/{actionType}")]
    public IActionResult GetPolicy(
        RemediationActionType actionType)
    {
        var policy =
            _policyEngine.GetPolicy(actionType);

        return policy is null
            ? NotFound()
            : Ok(policy);
    }

    [HttpGet("evaluate/{remediationId:guid}")]
    public async Task<IActionResult> Evaluate(
        Guid remediationId,
        CancellationToken cancellationToken)
    {
        var remediation =
            await _remediationService.GetByIdAsync(
                remediationId,
                cancellationToken);

        if (remediation is null)
        {
            return NotFound(new
            {
                message =
                    "Remediation action not found."
            });
        }

        return Ok(
            _policyEngine.Evaluate(remediation));
    }
}
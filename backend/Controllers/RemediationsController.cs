using backend.Agents.Remediation.Core;
using backend.Agents.Remediation.Services;
using backend.Models;
using backend.Realtime;
using backend.Services;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/remediations")]
public sealed class RemediationsController : ControllerBase
{
    private readonly RemediationService _remediationService;

    private readonly RemediationRunbookExecutor _runbookExecutor;

    private readonly RemediationLifecycleService _lifecycleService;

    private readonly RemediationVerificationWorkflowService _verificationWorkflow;
    private readonly IAiOpsEventPublisher _eventPublisher;
    private readonly ILogger<RemediationsController> _logger;

    public RemediationsController(
        RemediationService remediationService,
        RemediationLifecycleService lifecycleService,
        RemediationVerificationWorkflowService verificationWorkflow,
        RemediationRunbookExecutor runbookExecutor,
        IAiOpsEventPublisher eventPublisher,
        ILogger<RemediationsController> logger)
    {
        _remediationService = remediationService;
        _runbookExecutor = runbookExecutor;
        _lifecycleService = lifecycleService;
        _verificationWorkflow = verificationWorkflow;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    private async Task PublishRemediationUpdateAsync(
        RemediationAction? remediation)
    {
        if (remediation is null)
        {
            return;
        }

        try
        {
            await _eventPublisher.RemediationUpdatedAsync(remediation);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to publish real-time update for remediation {RemediationId}.",
                remediation.Id);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var remediations =
            await _remediationService.GetAllAsync(
                cancellationToken);

        return Ok(remediations);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var remediation =
            await _remediationService.GetByIdAsync(
                id,
                cancellationToken);

        return remediation is null
            ? NotFound()
            : Ok(remediation);
    }

    [HttpGet("incident/{incidentId:guid}")]
    public async Task<IActionResult> GetByIncident(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var remediations =
            await _remediationService
                .GetByIncidentIdAsync(
                    incidentId,
                    cancellationToken);

        return Ok(remediations);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRemediationRequest request,
        CancellationToken cancellationToken)
    {
        var remediation =
            await _remediationService.CreateAsync(
                request,
                cancellationToken);

        if (remediation is null)
        {
            return NotFound(new
            {
                message = "Incident not found."
            });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = remediation.Id },
            remediation);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRemediationRequest request,
        CancellationToken cancellationToken)
    {
        var remediation =
            await _remediationService.UpdateAsync(
                id,
                request,
                cancellationToken);

        return remediation is null
            ? NotFound()
            : Ok(remediation);
    }

    [HttpPost("{id:guid}/execute-runbook")]
    public async Task<IActionResult> ExecuteRunbook(
        Guid id,
        CancellationToken cancellationToken)
    {
        var remediation =
            await _remediationService.GetByIdAsync(
                id,
                cancellationToken);

        if (remediation is null)
        {
            return NotFound(new
            {
                message = "Remediation action not found."
            });
        }

        var result =
            await _runbookExecutor.ExecuteAsync(
                id,
                cancellationToken);

        await PublishRemediationUpdateAsync(
            await _remediationService.GetByIdAsync(id, cancellationToken));
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
    Guid id,
    [FromBody] ApproveRemediationRequest request,
    CancellationToken cancellationToken)
    {
        var operatorName = GetAuthenticatedOperator();
        if (operatorName is null)
        {
            return Forbid();
        }

        var result =
            await _lifecycleService.ApproveAsync(
                id,
                operatorName,
                cancellationToken);

        if (result.Success)
        {
            await PublishRemediationUpdateAsync(result.Remediation);
        }

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
    Guid id,
    [FromBody] RejectRemediationRequest request,
    CancellationToken cancellationToken)
    {
        var operatorName = GetAuthenticatedOperator();
        if (operatorName is null)
        {
            return Forbid();
        }

        var result =
            await _lifecycleService.RejectAsync(
                id,
                operatorName,
                request.Reason,
                cancellationToken);

        if (result.Success)
        {
            await PublishRemediationUpdateAsync(result.Remediation);
        }

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    private string? GetAuthenticatedOperator()
    {
        return User.FindFirst("name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst("preferred_username")?.Value;
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result =
            await _lifecycleService.ExecuteAsync(
                id,
                cancellationToken);

        await PublishRemediationUpdateAsync(
            await _remediationService.GetByIdAsync(id, cancellationToken));
        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(
      Guid id,
      CancellationToken cancellationToken)
    {
        var result =
            await _verificationWorkflow.VerifyAsync(
                id,
                cancellationToken);

        await PublishRemediationUpdateAsync(
            await _remediationService.GetByIdAsync(id, cancellationToken));
        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }
}
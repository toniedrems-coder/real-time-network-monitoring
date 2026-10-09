using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private static readonly string[] Severities = ["Low", "Medium", "High", "Critical"];

    private readonly MonitoringDbContext _db;
    private readonly EndpointService _endpoints;
    private readonly WorkflowNotificationService _notifications;

    public AlertsController(
        MonitoringDbContext db,
        EndpointService endpoints,
        WorkflowNotificationService notifications)
    {
        _db = db;
        _endpoints = endpoints;
        _notifications = notifications;
    }

    [HttpGet("transport")]
    public IActionResult GetTransport() => Ok(new
    {
        email = _notifications.IsEmailTransportConfigured,
        sms = _notifications.IsSmsTransportConfigured
    });

    // ---- contacts ----

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts(CancellationToken cancellationToken) =>
        Ok(await _db.SupportContacts.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken));

    [HttpPost("contacts")]
    public async Task<IActionResult> CreateContact(SupportContactRequest request, CancellationToken cancellationToken)
    {
        if (ValidateContact(request) is { } problem)
        {
            return problem;
        }

        var contact = new SupportContact();
        ApplyContact(contact, request);
        _db.SupportContacts.Add(contact);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(contact);
    }

    [HttpPut("contacts/{id:guid}")]
    public async Task<IActionResult> UpdateContact(Guid id, SupportContactRequest request, CancellationToken cancellationToken)
    {
        if (ValidateContact(request) is { } problem)
        {
            return problem;
        }

        var contact = await _db.SupportContacts.FindAsync([id], cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        ApplyContact(contact, request);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(contact);
    }

    [HttpDelete("contacts/{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _db.SupportContacts.FindAsync([id], cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        _db.SupportContacts.Remove(contact);
        // Rules keep working without the removed contact; stale ids are ignored at send time.
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---- rules ----

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken cancellationToken) =>
        Ok(await _db.AlertRules.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken));

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule(AlertRuleRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateRuleAsync(request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var rule = new AlertRule();
        ApplyRule(rule, request);
        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(rule);
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, AlertRuleRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateRuleAsync(request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var rule = await _db.AlertRules.FindAsync([id], cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        ApplyRule(rule, request);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(rule);
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken cancellationToken)
    {
        var rule = await _db.AlertRules.FindAsync([id], cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        _db.AlertRules.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---- history ----

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] int limit = 200, CancellationToken cancellationToken = default) =>
        Ok(await _db.AlertEvents.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken));

    // ---- helpers ----

    private IActionResult? ValidateContact(SupportContactRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required (max 100 characters).");
        }

        var email = request.Email?.Trim();
        var phone = request.Phone?.Trim();
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(phone))
        {
            ModelState.AddModelError(nameof(request.Email), "Provide an email address, a phone number, or both.");
        }

        if (!string.IsNullOrEmpty(email) && (email.Length > 254 || !WorkflowNotificationService.IsValidEmailAddress(email)))
        {
            ModelState.AddModelError(nameof(request.Email), "Email address is not valid.");
        }

        if (!string.IsNullOrEmpty(phone) && !WorkflowNotificationService.IsValidPhoneNumber(phone))
        {
            ModelState.AddModelError(nameof(request.Phone), "Phone must be in international E.164 format, e.g. +2348012345678.");
        }

        return ModelState.IsValid ? null : ValidationProblem(ModelState);
    }

    private static void ApplyContact(SupportContact contact, SupportContactRequest request)
    {
        contact.Name = request.Name!.Trim();
        contact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        contact.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        contact.Active = request.Active;
    }

    private async Task<IActionResult?> ValidateRuleAsync(AlertRuleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required (max 100 characters, no control characters).");
        }

        if (request.TargetType == AlertTargetTypes.Endpoint)
        {
            if (!Guid.TryParse(request.TargetId, out var endpointId) || _endpoints.GetById(endpointId) is null)
            {
                ModelState.AddModelError(nameof(request.TargetId), "Select an existing endpoint.");
            }

            if (request.Metric is null || !AlertMetrics.EndpointMetrics.Contains(request.Metric))
            {
                ModelState.AddModelError(nameof(request.Metric), "Metric must be Latency, ErrorRate, Availability or Unreachable for endpoints.");
            }
        }
        else if (request.TargetType == AlertTargetTypes.Container)
        {
            var target = request.TargetId?.Trim();
            if (string.IsNullOrEmpty(target) || target.Length > 200 || target.Any(char.IsControl))
            {
                ModelState.AddModelError(nameof(request.TargetId), "Container name is required.");
            }

            if (request.Metric != AlertMetrics.ContainerNotRunning)
            {
                ModelState.AddModelError(nameof(request.Metric), "Containers support the ContainerNotRunning metric.");
            }
        }
        else
        {
            ModelState.AddModelError(nameof(request.TargetType), "Target type must be Endpoint or Container.");
        }

        var needsThreshold = request.Metric is AlertMetrics.Latency or AlertMetrics.ErrorRate or AlertMetrics.Availability;
        if (needsThreshold)
        {
            var max = request.Metric == AlertMetrics.Latency ? 600_000 : 100;
            if (double.IsNaN(request.Threshold) || request.Threshold <= 0 || request.Threshold > max)
            {
                ModelState.AddModelError(nameof(request.Threshold), $"Threshold must be greater than 0 and at most {max}.");
            }
        }

        if (request.DurationMinutes is < 0 or > 1440)
        {
            ModelState.AddModelError(nameof(request.DurationMinutes), "Duration must be between 0 and 1440 minutes.");
        }

        if (request.CooldownMinutes is < 0 or > 10080)
        {
            ModelState.AddModelError(nameof(request.CooldownMinutes), "Repeat interval must be between 0 and 10080 minutes.");
        }

        if (request.Severity is null || !Severities.Contains(request.Severity))
        {
            ModelState.AddModelError(nameof(request.Severity), "Severity must be Low, Medium, High or Critical.");
        }

        if (!request.NotifyEmail && !request.NotifySms)
        {
            ModelState.AddModelError(nameof(request.NotifyEmail), "Choose email, SMS or both.");
        }

        var ids = request.ContactIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0 || ids.Count > 50)
        {
            ModelState.AddModelError(nameof(request.ContactIds), "Select between 1 and 50 support contacts.");
        }
        else
        {
            var found = await _db.SupportContacts.CountAsync(c => ids.Contains(c.Id), cancellationToken);
            if (found != ids.Count)
            {
                ModelState.AddModelError(nameof(request.ContactIds), "One or more selected contacts do not exist.");
            }
        }

        return ModelState.IsValid ? null : ValidationProblem(ModelState);
    }

    private static void ApplyRule(AlertRule rule, AlertRuleRequest request)
    {
        rule.Name = request.Name!.Trim();
        rule.TargetType = request.TargetType!;
        rule.TargetId = request.TargetId!.Trim();
        rule.Metric = request.Metric!;
        rule.Threshold = request.Metric is AlertMetrics.Latency or AlertMetrics.ErrorRate or AlertMetrics.Availability
            ? request.Threshold
            : 0;
        rule.DurationMinutes = request.DurationMinutes;
        rule.Severity = request.Severity!;
        rule.NotifyEmail = request.NotifyEmail;
        rule.NotifySms = request.NotifySms;
        rule.ContactIds = request.ContactIds!.Distinct().ToList();
        rule.CooldownMinutes = request.CooldownMinutes;
        rule.Enabled = request.Enabled;

        // Any edit restarts breach tracking so the new thresholds are judged afresh.
        rule.BreachStartedAt = null;
        rule.Firing = false;
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Models;

namespace backend.Services;

public sealed class WebhookNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public WebhookNotificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task NotifyWorkflowCompletedAsync(
        WorkflowExecution workflow,
        Incident incident,
        RemediationAction remediation,
        CancellationToken cancellationToken = default)
    {
        var configuredUrl = _configuration["Notifications:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return;
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var webhookUri) ||
            (webhookUri.Scheme != Uri.UriSchemeHttps &&
             !(_configuration.GetValue<bool>("Notifications:AllowInsecureHttp") &&
               webhookUri.IsLoopback)))
        {
            throw new InvalidOperationException(
                "Notifications:WebhookUrl must be an absolute HTTPS URL; HTTP is permitted only for loopback development when Notifications:AllowInsecureHttp=true.");
        }

        var secret = _configuration["Notifications:WebhookSigningSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "Notifications:WebhookSigningSecret is required when a notification webhook is configured.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            eventId = workflow.Id,
            eventType = "incident.workflow.completed",
            occurredAt = workflow.CompletedAt ?? DateTimeOffset.UtcNow,
            incident = new
            {
                incident.Id,
                incident.IncidentNumber,
                incident.Title,
                severity = incident.Severity.ToString(),
                status = incident.Status.ToString(),
                incident.Target,
                incident.RootCause
            },
            remediation = new
            {
                remediation.Id,
                remediation.RemediationNumber,
                action = remediation.ActionType.ToString(),
                status = remediation.Status.ToString(),
                approval = remediation.ApprovalStatus.ToString()
            }
        });
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", workflow.Id.ToString());
        request.Headers.Add("X-Network-Monitor-Signature", $"sha256={signature}");
        using var response = await _httpClientFactory
            .CreateClient(nameof(WebhookNotificationService))
            .SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Notification webhook returned {(int)response.StatusCode}: {responseBody}");
        }
    }
}

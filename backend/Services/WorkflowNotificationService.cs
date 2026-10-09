using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Text;
using backend.Models;

namespace backend.Services;

public sealed record NotificationChannelStatus(string Status, string? Destination);

public sealed record NotificationConfigurationStatus(
    NotificationChannelStatus Email,
    NotificationChannelStatus Sms,
    NotificationChannelStatus Webhook);

public sealed record NotificationDeliverySummary(
    string EmailStatus,
    string SmsStatus,
    string? Error);

public sealed class WorkflowNotificationService
{
    private static readonly Regex E164PhonePattern = new(
        @"^\+[1-9]\d{7,14}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WorkflowNotificationService> _logger;

    public WorkflowNotificationService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<WorkflowNotificationService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public NotificationConfigurationStatus GetConfigurationStatus()
    {
        var emailReady =
            !string.IsNullOrWhiteSpace(_configuration["Notifications:Email:SmtpHost"]) &&
            HasValidAddress(_configuration["Notifications:Email:From"]) &&
            HasValidAddress(_configuration["Notifications:Email:To"]);
        var smsReady =
            !string.IsNullOrWhiteSpace(_configuration["Notifications:Sms:AccountSid"]) &&
            !string.IsNullOrWhiteSpace(_configuration["Notifications:Sms:AuthToken"]) &&
            IsValidPhone(_configuration["Notifications:Sms:From"]) &&
            IsValidPhone(_configuration["Notifications:Sms:To"]);
        var webhookUrl = _configuration["Notifications:WebhookUrl"];
        var webhookSecret = _configuration["Notifications:WebhookSigningSecret"];
        var webhookStatus = string.IsNullOrWhiteSpace(webhookUrl) && string.IsNullOrWhiteSpace(webhookSecret)
            ? "NotConfigured"
            : !string.IsNullOrWhiteSpace(webhookUrl) && !string.IsNullOrWhiteSpace(webhookSecret)
                ? "Configured"
                : "Incomplete";

        return new NotificationConfigurationStatus(
            new NotificationChannelStatus(
                emailReady ? "Configured" : "NotConfigured",
                emailReady ? MaskEmail(_configuration["Notifications:Email:To"]!) : null),
            new NotificationChannelStatus(
                smsReady ? "Configured" : "NotConfigured",
                smsReady ? MaskPhone(_configuration["Notifications:Sms:To"]!) : null),
            new NotificationChannelStatus(webhookStatus, null));
    }

    public async Task<NotificationDeliverySummary> NotifyAsync(
        WorkflowExecution workflow,
        Incident incident,
        RemediationAction remediation,
        CancellationToken cancellationToken = default)
    {
        var emailStatus = "NotConfigured";
        var smsStatus = "NotConfigured";
        var errors = new List<string>();
        var configuration = GetConfigurationStatus();

        if (configuration.Email.Status == "Configured")
        {
            try
            {
                await SendEmailAsync(incident, remediation, cancellationToken);
                emailStatus = "Sent";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                emailStatus = "Failed";
                errors.Add("Email delivery failed; see backend logs.");
                _logger.LogError(exception, "Email alert failed for workflow {WorkflowId}.", workflow.Id);
            }
        }

        if (configuration.Sms.Status == "Configured")
        {
            try
            {
                await SendSmsAsync(incident, remediation, cancellationToken);
                smsStatus = "Sent";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                smsStatus = "Failed";
                errors.Add("SMS delivery failed; see backend logs.");
                _logger.LogError(exception, "SMS alert failed for workflow {WorkflowId}.", workflow.Id);
            }
        }

        return new NotificationDeliverySummary(
            emailStatus,
            smsStatus,
            errors.Count == 0 ? null : string.Join(" ", errors));
    }

    public async Task SendTestAsync(string channel, CancellationToken cancellationToken)
    {
        var incident = new Incident
        {
            IncidentNumber = "TEST",
            Title = "Network Monitor notification test",
            Severity = IncidentSeverity.P4,
            Target = "Notification configuration",
            RootCause = "This is a test message."
        };
        var remediation = new RemediationAction
        {
            RemediationNumber = "TEST",
            ActionType = RemediationActionType.RecheckEndpoint,
            Status = RemediationStatus.Proposed
        };

        switch (channel.Trim().ToLowerInvariant())
        {
            case "email":
                if (GetConfigurationStatus().Email.Status != "Configured")
                {
                    throw new InvalidOperationException("Email notifications are not configured.");
                }

                await SendEmailAsync(incident, remediation, cancellationToken);
                break;
            case "sms":
                if (GetConfigurationStatus().Sms.Status != "Configured")
                {
                    throw new InvalidOperationException("SMS notifications are not configured.");
                }

                await SendSmsAsync(incident, remediation, cancellationToken);
                break;
            default:
                throw new ArgumentException("Channel must be 'email' or 'sms'.", nameof(channel));
        }
    }

    public bool IsEmailTransportConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Notifications:Email:SmtpHost"]) &&
        HasValidAddress(_configuration["Notifications:Email:From"]);

    public bool IsSmsTransportConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Notifications:Sms:AccountSid"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Notifications:Sms:AuthToken"]) &&
        IsValidPhone(_configuration["Notifications:Sms:From"]);

    public static bool IsValidEmailAddress(string? value) => HasValidAddress(value);

    public static bool IsValidPhoneNumber(string? value) => IsValidPhone(value);

    private Task SendEmailAsync(
        Incident incident,
        RemediationAction remediation,
        CancellationToken cancellationToken) =>
        SendEmailToAsync(
            _configuration["Notifications:Email:To"],
            $"[{incident.Severity}] {incident.IncidentNumber}: {incident.Title}",
            BuildMessage(incident, remediation),
            cancellationToken);

    private Task SendSmsAsync(
        Incident incident,
        RemediationAction remediation,
        CancellationToken cancellationToken) =>
        SendSmsToAsync(
            _configuration["Notifications:Sms:To"],
            BuildMessage(incident, remediation),
            cancellationToken);

    public async Task SendEmailToAsync(
        string? to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var host = _configuration["Notifications:Email:SmtpHost"];
        var from = _configuration["Notifications:Email:From"];
        if (string.IsNullOrWhiteSpace(host) || !HasValidAddress(from) || !HasValidAddress(to))
        {
            throw new InvalidOperationException("SMTP host, sender, and recipient are required.");
        }

        var port = _configuration.GetValue("Notifications:Email:SmtpPort", 587);
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("SMTP port must be between 1 and 65535.");
        }

        using var message = new MailMessage(from!, to!)
        {
            Subject = subject.ReplaceLineEndings(" "),
            Body = body,
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = _configuration.GetValue("Notifications:Email:UseTls", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };
        var username = _configuration["Notifications:Email:UserName"];
        var password = _configuration["Notifications:Email:Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("SMTP password is required when a username is configured.");
            }

            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendSmsToAsync(
        string? to,
        string body,
        CancellationToken cancellationToken)
    {
        var accountSid = _configuration["Notifications:Sms:AccountSid"];
        var authToken = _configuration["Notifications:Sms:AuthToken"];
        var from = _configuration["Notifications:Sms:From"];
        if (string.IsNullOrWhiteSpace(accountSid) ||
            string.IsNullOrWhiteSpace(authToken) ||
            !IsValidPhone(from) ||
            !IsValidPhone(to))
        {
            throw new InvalidOperationException("Twilio account SID, auth token, sender, and recipient are required.");
        }

        var destination = new Uri(
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(accountSid)}/Messages.json");
        using var request = new HttpRequestMessage(HttpMethod.Post, destination);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = from!,
            ["To"] = to!,
            ["Body"] = body
        });

        using var response = await _httpClientFactory
            .CreateClient(nameof(WorkflowNotificationService))
            .SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"SMS provider returned HTTP {(int)response.StatusCode}: {responseContent}");
        }
    }
    private static string BuildMessage(Incident incident, RemediationAction remediation) =>
        $"[{incident.Severity}] {incident.IncidentNumber}: {incident.Title}. " +
        $"Target: {incident.Target}. RCA: {incident.RootCause ?? "pending"}. " +
        $"Remediation: {remediation.ActionType} ({remediation.Status}).";

    private static bool HasValidAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        MailAddress.TryCreate(value, out _);

    private static bool IsValidPhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) && E164PhonePattern.IsMatch(value);

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" + email[at..] : email[..1] + "***" + email[at..];
    }

    private static string MaskPhone(string phone) =>
        phone.Length <= 4 ? "****" : new string('*', phone.Length - 4) + phone[^4..];
}
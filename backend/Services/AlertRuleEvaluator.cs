using System.Globalization;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed record AlertEvaluation(bool? Breached, string Detail);

public sealed class AlertRuleEvaluator : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxMetricAge = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MetricsStore _metrics;
    private readonly EndpointService _endpoints;
    private readonly DockerMonitoringService _docker;
    private readonly ILogger<AlertRuleEvaluator> _logger;

    public AlertRuleEvaluator(
        IServiceScopeFactory scopeFactory,
        MetricsStore metrics,
        EndpointService endpoints,
        DockerMonitoringService docker,
        ILogger<AlertRuleEvaluator> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _endpoints = endpoints;
        _docker = docker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _endpoints.WaitUntilInitializedAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await EvaluateAllAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Alert rule evaluation cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static AlertEvaluation EvaluateEndpoint(AlertRule rule, EndpointMetrics? latest, DateTimeOffset now)
    {
        if (latest is null || now - latest.Timestamp > MaxMetricAge)
        {
            return new AlertEvaluation(null, "no recent metrics");
        }

        var inv = CultureInfo.InvariantCulture;
        return rule.Metric switch
        {
            AlertMetrics.Latency => new AlertEvaluation(
                latest.LatencyMs > rule.Threshold,
                string.Create(inv, $"latency {latest.LatencyMs:F0} ms (threshold {rule.Threshold:G} ms)")),
            AlertMetrics.ErrorRate => new AlertEvaluation(
                latest.ErrorRatePercent > rule.Threshold,
                string.Create(inv, $"error rate {latest.ErrorRatePercent:F1}% (threshold {rule.Threshold:G}%)")),
            AlertMetrics.Availability => new AlertEvaluation(
                latest.AvailabilityPercent < rule.Threshold,
                string.Create(inv, $"availability {latest.AvailabilityPercent:F1}% (minimum {rule.Threshold:G}%)")),
            AlertMetrics.Unreachable => new AlertEvaluation(
                !latest.IsReachable,
                latest.IsReachable ? "reachable" : "endpoint is unreachable"),
            _ => new AlertEvaluation(null, "unsupported metric")
        };
    }

    public static AlertEvaluation EvaluateContainer(string containerName, ContainerSummary? container)
    {
        if (container is null)
        {
            return new AlertEvaluation(true, $"container {containerName} was not found");
        }

        var running = string.Equals(container.State, "running", StringComparison.OrdinalIgnoreCase);
        return new AlertEvaluation(!running, $"container state is {container.State}");
    }

    private async Task EvaluateAllAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
        var rules = await db.AlertRules.Where(rule => rule.Enabled).ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return;
        }

        IReadOnlyList<ContainerSummary>? containers = null;
        var dockerFailed = false;

        foreach (var rule in rules)
        {
            string targetName;
            AlertEvaluation evaluation;

            if (rule.TargetType == AlertTargetTypes.Endpoint)
            {
                if (!Guid.TryParse(rule.TargetId, out var endpointId) || _endpoints.GetById(endpointId) is not { } endpoint)
                {
                    continue;
                }

                targetName = endpoint.Name;
                evaluation = EvaluateEndpoint(rule, _metrics.GetLatest(endpointId), now);
            }
            else
            {
                if (dockerFailed)
                {
                    continue;
                }

                if (containers is null)
                {
                    try
                    {
                        containers = await _docker.GetContainersAsync(cancellationToken);
                    }
                    catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                    {
                        dockerFailed = true;
                        _logger.LogWarning("Container alert rules skipped; Docker API unavailable: {Message}", exception.Message);
                        continue;
                    }
                }

                targetName = rule.TargetId;
                var match = containers.FirstOrDefault(c =>
                    string.Equals(c.Names?.Trim('/'), rule.TargetId.Trim('/'), StringComparison.OrdinalIgnoreCase));
                evaluation = EvaluateContainer(rule.TargetId, match);
            }

            if (evaluation.Breached is null)
            {
                continue;
            }

            await ApplyAsync(scope.ServiceProvider, db, rule, targetName, evaluation, now, cancellationToken);
        }
    }

    private async Task ApplyAsync(
        IServiceProvider services,
        MonitoringDbContext db,
        AlertRule rule,
        string targetName,
        AlertEvaluation evaluation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string? kind = null;

        if (evaluation.Breached == true)
        {
            rule.BreachStartedAt ??= now;
            if (now - rule.BreachStartedAt.Value >= TimeSpan.FromMinutes(rule.DurationMinutes))
            {
                if (!rule.Firing)
                {
                    rule.Firing = true;
                    kind = "Triggered";
                }
                else if (rule.CooldownMinutes > 0 &&
                    now - (rule.LastNotifiedAt ?? DateTimeOffset.MinValue) >= TimeSpan.FromMinutes(rule.CooldownMinutes))
                {
                    kind = "Reminder";
                }
            }
        }
        else
        {
            rule.BreachStartedAt = null;
            if (rule.Firing)
            {
                rule.Firing = false;
                kind = "Resolved";
            }
        }

        if (kind is not null)
        {
            rule.LastNotifiedAt = now;
            await NotifyAsync(services, db, rule, targetName, kind, evaluation.Detail, now, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyAsync(
        IServiceProvider services,
        MonitoringDbContext db,
        AlertRule rule,
        string targetName,
        string kind,
        string detail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var notifier = services.GetRequiredService<WorkflowNotificationService>();
        var contactIds = rule.ContactIds.ToList();
        var contacts = await db.SupportContacts
            .Where(contact => contact.Active && contactIds.Contains(contact.Id))
            .ToListAsync(cancellationToken);

        var headline = $"[{rule.Severity.ToUpperInvariant()}] {(kind == "Resolved" ? "RESOLVED" : "ALERT")}: {rule.Name} - {targetName}";
        var message = kind == "Resolved"
            ? $"{headline}. Back to normal ({detail})."
            : $"{headline}. {char.ToUpperInvariant(detail[0])}{detail[1..]}{(kind == "Reminder" ? " (still active)" : string.Empty)}.";

        var deliveries = new List<AlertDelivery>();
        foreach (var contact in contacts)
        {
            if (rule.NotifyEmail && !string.IsNullOrWhiteSpace(contact.Email))
            {
                deliveries.Add(await DeliverAsync(contact.Name, "Email", notifier.IsEmailTransportConfigured,
                    () => notifier.SendEmailToAsync(contact.Email, headline, message, cancellationToken)));
            }

            if (rule.NotifySms && !string.IsNullOrWhiteSpace(contact.Phone))
            {
                var sms = message.Length > 300 ? message[..300] : message;
                deliveries.Add(await DeliverAsync(contact.Name, "SMS", notifier.IsSmsTransportConfigured,
                    () => notifier.SendSmsToAsync(contact.Phone, sms, cancellationToken)));
            }
        }

        db.AlertEvents.Add(new AlertEvent
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Target = targetName,
            Kind = kind,
            Severity = rule.Severity,
            Message = message,
            CreatedAt = now,
            Deliveries = deliveries
        });
    }

    private async Task<AlertDelivery> DeliverAsync(string contact, string channel, bool transportReady, Func<Task> send)
    {
        if (!transportReady)
        {
            return new AlertDelivery(contact, channel, "NotConfigured", $"{channel} delivery is not configured on the server.");
        }

        try
        {
            await send();
            return new AlertDelivery(contact, channel, "Sent", null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Alert {Channel} delivery to {Contact} failed.", channel, contact);
            return new AlertDelivery(contact, channel, "Failed", "Delivery failed; see backend logs.");
        }
    }
}

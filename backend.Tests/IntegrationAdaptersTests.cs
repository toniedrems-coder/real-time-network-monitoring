using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using backend.Agents.Remediation.Core;
using backend.Agents.Remediation.Models;
using backend.Models;
using backend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests;

public sealed class IntegrationAdaptersTests
{
    [Fact]
    public void AlertRulesEvaluateEndpointMetricsAgainstThresholds()
    {
        var now = DateTimeOffset.UtcNow;
        var metrics = new EndpointMetrics(Guid.NewGuid(), now, 950, 0, 99.5, 7, 1, true);

        var latency = new AlertRule { Metric = AlertMetrics.Latency, Threshold = 800 };
        Assert.True(AlertRuleEvaluator.EvaluateEndpoint(latency, metrics, now).Breached);
        Assert.False(AlertRuleEvaluator.EvaluateEndpoint(new AlertRule { Metric = AlertMetrics.Latency, Threshold = 1000 }, metrics, now).Breached);
        Assert.True(AlertRuleEvaluator.EvaluateEndpoint(new AlertRule { Metric = AlertMetrics.ErrorRate, Threshold = 5 }, metrics, now).Breached);
        Assert.False(AlertRuleEvaluator.EvaluateEndpoint(new AlertRule { Metric = AlertMetrics.Availability, Threshold = 99 }, metrics, now).Breached);
        Assert.True(AlertRuleEvaluator.EvaluateEndpoint(new AlertRule { Metric = AlertMetrics.Unreachable }, metrics with { IsReachable = false }, now).Breached);
    }

    [Fact]
    public void AlertRulesIgnoreMissingOrStaleMetrics()
    {
        var now = DateTimeOffset.UtcNow;
        var rule = new AlertRule { Metric = AlertMetrics.Latency, Threshold = 1 };
        var stale = new EndpointMetrics(Guid.NewGuid(), now.AddHours(-1), 5000, 0, 100, 0, 1, true);

        Assert.Null(AlertRuleEvaluator.EvaluateEndpoint(rule, null, now).Breached);
        Assert.Null(AlertRuleEvaluator.EvaluateEndpoint(rule, stale, now).Breached);
    }

    [Fact]
    public void ContainerAlertsTreatMissingOrStoppedContainersAsBreached()
    {
        var running = new ContainerSummary("abc", "db", "postgres", "running", "Up", [], null);

        Assert.False(AlertRuleEvaluator.EvaluateContainer("db", running).Breached);
        Assert.True(AlertRuleEvaluator.EvaluateContainer("db", running with { State = "exited" }).Breached);
        Assert.True(AlertRuleEvaluator.EvaluateContainer("db", null).Breached);
    }
    [Fact]
    public void LogDiscoveryExtractsOnlyMonitorableIpv4Addresses()
    {
        const string log = "conn from 10.0.0.5:443 to 8.8.8.8, lo 127.0.0.1 meta 169.254.169.254 v1.2.3.4.5 ver 999.1.1.1 multicast 224.0.0.1 08.8.8.8";
        var ips = LogIpDiscoveryService.ExtractIps(log).ToArray();

        Assert.Equal(new[] { "10.0.0.5", "8.8.8.8" }, ips);
        Assert.True(LogIpDiscoveryService.IsPrivate("10.0.0.5"));
        Assert.False(LogIpDiscoveryService.IsPrivate("8.8.8.8"));
    }
    [Fact]
    public async Task RestartPodUsesNamespacedDeleteAndBearerToken()
    {
        using var handler = new RecordingHandler();
        var operations = new KubernetesOperations(
            new StubHttpClientFactory(handler),
            Configuration(("Kubernetes:ApiServer", "https://kubernetes.example"),
                ("Kubernetes:BearerToken", "test-token")));

        await operations.RestartPodAsync("monitoring/api-7f86", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Method);
        Assert.Equal(
            new Uri("https://kubernetes.example/api/v1/namespaces/monitoring/pods/api-7f86"),
            handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-token", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task KubernetesTargetRejectsPathInjectionBeforeSendingRequest()
    {
        using var handler = new RecordingHandler();
        var operations = new KubernetesOperations(
            new StubHttpClientFactory(handler),
            Configuration(("Kubernetes:ApiServer", "https://kubernetes.example"),
                ("Kubernetes:BearerToken", "test-token")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            operations.RestartPodAsync("monitoring/../secrets", CancellationToken.None));

        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task ScaleDeploymentStopsAtConfiguredReplicaLimit()
    {
        using var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"spec":{"replicas":4}}""")
            });
        var operations = new KubernetesOperations(
            new StubHttpClientFactory(handler),
            Configuration(("Kubernetes:ApiServer", "https://kubernetes.example"),
                ("Kubernetes:BearerToken", "test-token"),
                ("Kubernetes:MaximumReplicas", "4")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            operations.ScaleDeploymentAsync("monitoring/api", CancellationToken.None));

        Assert.Equal(HttpMethod.Get, handler.Method);
    }

    [Fact]
    public async Task WebhookIsSignedAndIncludesWorkflowIdempotencyKey()
    {
        using var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted));
        var secret = "local-test-secret";
        var workflowId = Guid.NewGuid();
        var notifier = new WebhookNotificationService(
            new StubHttpClientFactory(handler),
            Configuration(("Notifications:WebhookUrl", "https://notify.example/hooks"),
                ("Notifications:WebhookSigningSecret", secret)));

        await notifier.NotifyWorkflowCompletedAsync(
            new WorkflowExecution { Id = workflowId },
            new Incident { IncidentNumber = "INC-123" },
            new RemediationAction { RemediationNumber = "REM-123" },
            CancellationToken.None);

        Assert.Equal(workflowId.ToString(), handler.IdempotencyKey);
        var expected = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(handler.Body!)))
            .ToLowerInvariant();
        Assert.Equal($"sha256={expected}", handler.Signature);
        Assert.Contains(workflowId.ToString(), handler.Body);
    }

    [Fact]
    public async Task DockerContainerListUsesConfiguredApiAndParsesSummary()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """[{"Id":"0123456789abcdef","Names":["/api"],"Image":"network-api:latest","State":"running","Status":"Up 1 minute","Created":1700000000,"Ports":[{"PrivatePort":8080,"Type":"tcp","PublicPort":18080}]}]""")
        });
        var docker = new DockerMonitoringService(
            new StubHttpClientFactory(handler),
            Configuration(("Docker:ApiBaseUrl", "https://docker-proxy.example/v1.45/"),
                ("Docker:BearerToken", "docker-token")));

        var containers = await docker.GetContainersAsync();

        var container = Assert.Single(containers);
        Assert.Equal("api", container.Names);
        Assert.Equal("running", container.State);
        Assert.Equal("18080:8080/tcp", Assert.Single(container.Ports));
        Assert.Equal(new Uri("https://docker-proxy.example/v1.45/containers/json?all=1&limit=500"), handler.RequestUri);
        Assert.Equal("docker-token", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task DockerLogTailRejectsInvalidContainerIdBeforeRequest()
    {
        using var handler = new RecordingHandler();
        var docker = new DockerMonitoringService(
            new StubHttpClientFactory(handler),
            Configuration(("Docker:ApiBaseUrl", "https://docker-proxy.example")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            docker.GetLogsAsync("../../../etc/passwd"));

        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task DockerLogTailIsCappedAndDecodesMultiplexedOutput()
    {
        var log = Encoding.UTF8.GetBytes("2026-10-05T12:00:00Z container started\n");
        var frame = new byte[8 + log.Length];
        frame[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4, 4), log.Length);
        log.CopyTo(frame.AsSpan(8));
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(frame)
        });
        var docker = new DockerMonitoringService(
            new StubHttpClientFactory(handler),
            Configuration(("Docker:ApiBaseUrl", "https://docker-proxy.example")));

        var result = await docker.GetLogsAsync("0123456789abcdef", tail: 10000);

        Assert.Equal(1000, result.Tail);
        Assert.Equal("2026-10-05T12:00:00Z container started\n", result.Content);
        Assert.Contains("tail=1000", handler.RequestUri!.Query);
    }

    [Fact]
    public async Task DockerRejectsNonLoopbackHttpEndpoints()
    {
        using var handler = new RecordingHandler();
        var docker = new DockerMonitoringService(
            new StubHttpClientFactory(handler),
            Configuration(("Docker:ApiBaseUrl", "http://docker.example")));

        Assert.Equal("NotConfigured", docker.GetConnectionStatus().Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => docker.GetContainersAsync());
        Assert.Null(handler.Method);
    }

    [Fact]
    public void NotificationConfigurationRequiresValidEmailAndE164Phone()
    {
        var notifications = new WorkflowNotificationService(
            Configuration(
                ("Notifications:Email:SmtpHost", "smtp.example"),
                ("Notifications:Email:From", "alerts@example.com"),
                ("Notifications:Email:To", "oncall@example.com"),
                ("Notifications:Sms:AccountSid", "account"),
                ("Notifications:Sms:AuthToken", "token"),
                ("Notifications:Sms:From", "+15551234567"),
                ("Notifications:Sms:To", "+1not-a-number"),
                ("Notifications:WebhookUrl", "https://hooks.example"),
                ("Notifications:WebhookSigningSecret", "secret")),
            new StubHttpClientFactory(new RecordingHandler()),
            NullLogger<WorkflowNotificationService>.Instance);

        var status = notifications.GetConfigurationStatus();

        Assert.Equal("Configured", status.Email.Status);
        Assert.Equal("NotConfigured", status.Sms.Status);
        Assert.Equal("Configured", status.Webhook.Status);
        Assert.DoesNotContain("oncall@example.com", status.Email.Destination);
    }

    private static IConfiguration Configuration(
        params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage>? responseFactory = null)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Signature { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var idempotency)
                ? idempotency.Single()
                : null;
            Signature = request.Headers.TryGetValues("X-Network-Monitor-Signature", out var signature)
                ? signature.Single()
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory?.Invoke(request)
                ?? new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Agents.Remediation.Abstractions;

namespace backend.Agents.Remediation.Core;

public sealed class KubernetesOperations : IKubernetesOperations
{
    private static readonly Regex TargetPattern = new(
        "^(?<namespace>[a-z0-9](?:[-a-z0-9]*[a-z0-9])?)/(?<name>[a-z0-9](?:[-a-z0-9.]*[a-z0-9])?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public KubernetesOperations(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public Task RestartPodAsync(string target, CancellationToken cancellationToken)
    {
        var (ns, name) = ParseTarget(target);
        return SendAsync(
            HttpMethod.Delete,
            $"api/v1/namespaces/{ns}/pods/{name}",
            null,
            cancellationToken);
    }

    public Task RestartDeploymentAsync(string target, CancellationToken cancellationToken)
    {
        var (ns, name) = ParseTarget(target);
        var body = JsonSerializer.Serialize(new
        {
            spec = new
            {
                template = new
                {
                    metadata = new
                    {
                        annotations = new Dictionary<string, string>
                        {
                            ["network-monitor/restarted-at"] = DateTimeOffset.UtcNow.ToString("O")
                        }
                    }
                }
            }
        });

        return SendAsync(
            HttpMethod.Patch,
            $"apis/apps/v1/namespaces/{ns}/deployments/{name}",
            body,
            cancellationToken,
            "application/merge-patch+json");
    }

    public async Task ScaleDeploymentAsync(string target, CancellationToken cancellationToken)
    {
        var (ns, name) = ParseTarget(target);
        var client = CreateClient();
        using var getResponse = await client.GetAsync(
            $"apis/apps/v1/namespaces/{ns}/deployments/{name}",
            cancellationToken);
        var responseBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!getResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Kubernetes deployment lookup failed with {(int)getResponse.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var currentReplicas = document.RootElement
            .GetProperty("spec")
            .TryGetProperty("replicas", out var replicas)
                ? replicas.GetInt32()
                : 1;
        var maximumReplicas = _configuration.GetValue("Kubernetes:MaximumReplicas", 10);
        if (maximumReplicas < 1 || currentReplicas >= maximumReplicas)
        {
            throw new InvalidOperationException(
                $"Deployment has {currentReplicas} replicas and configured maximum is {maximumReplicas}; scaling is blocked.");
        }

        var body = JsonSerializer.Serialize(new
        {
            spec = new { replicas = currentReplicas + 1 }
        });
        await SendAsync(
            HttpMethod.Patch,
            $"apis/apps/v1/namespaces/{ns}/deployments/{name}",
            body,
            cancellationToken,
            "application/merge-patch+json");
    }

    public static bool IsValidTarget(string target) => TargetPattern.IsMatch(target);

    private HttpClient CreateClient()
    {
        var configuredServer = _configuration["Kubernetes:ApiServer"];
        var token = _configuration["Kubernetes:BearerToken"];
        if (!Uri.TryCreate(configuredServer, UriKind.Absolute, out var server) ||
            (server.Scheme != Uri.UriSchemeHttps &&
             !(_configuration.GetValue<bool>("Kubernetes:AllowInsecureHttp") &&
               server.IsLoopback)))
        {
            throw new InvalidOperationException(
                "Kubernetes:ApiServer must be an absolute HTTPS URL. HTTP is permitted only for loopback development with Kubernetes:AllowInsecureHttp=true.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Kubernetes:BearerToken is required before Kubernetes actions can run.");
        }

        var client = _httpClientFactory.CreateClient(nameof(KubernetesOperations));
        client.BaseAddress = new Uri(server.ToString().TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SendAsync(
        HttpMethod method,
        string path,
        string? body,
        CancellationToken cancellationToken,
        string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        using var response = await CreateClient().SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Kubernetes API request failed with {(int)response.StatusCode}: {responseBody}");
        }
    }

    private static (string Namespace, string Name) ParseTarget(string target)
    {
        var match = TargetPattern.Match(target);
        if (!match.Success)
        {
            throw new ArgumentException(
                "Kubernetes target must use the namespace/resource-name format.",
                nameof(target));
        }

        return (match.Groups["namespace"].Value, match.Groups["name"].Value);
    }
}

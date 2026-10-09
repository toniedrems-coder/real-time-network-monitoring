using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace backend.Services;

public sealed record ContainerSummary(
    string Id,
    string Names,
    string Image,
    string State,
    string Status,
    string[] Ports,
    DateTimeOffset? CreatedAt);

public sealed record ContainerLogs(string ContainerId, int Tail, string Content);
public sealed record DockerConnectionStatus(string Status);

public sealed class DockerMonitoringService
{
    private static readonly Regex ContainerIdPattern = new(
        "^[a-fA-F0-9]{12,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DockerMonitoringService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<ContainerSummary>> GetContainersAsync(
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var client = CreateClient();
        using var response = await client.GetAsync(
            "containers/json?all=1&limit=500",
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        var responseBody = await ReadBoundedAsync(
            await response.Content.ReadAsStreamAsync(timeout.Token),
            2 * 1024 * 1024,
            timeout.Token);
        EnsureSuccess(response, responseBody);

        using var document = JsonDocument.Parse(responseBody);
        var result = new List<ContainerSummary>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var createdSeconds = item.TryGetProperty("Created", out var created)
                ? created.GetInt64()
                : 0;
            var ports = item.TryGetProperty("Ports", out var portList) &&
                        portList.ValueKind == JsonValueKind.Array
                ? portList.EnumerateArray().Select(port =>
                {
                    var privatePort = port.GetProperty("PrivatePort").GetInt32();
                    var protocol = port.GetProperty("Type").GetString();
                    return port.TryGetProperty("PublicPort", out var publicPort)
                        ? $"{publicPort.GetInt32()}:{privatePort}/{protocol}"
                        : $"{privatePort}/{protocol}";
                }).ToArray()
                : [];

            result.Add(new ContainerSummary(
                item.GetProperty("Id").GetString() ?? string.Empty,
                item.TryGetProperty("Names", out var names) &&
                names.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", names.EnumerateArray().Select(name => name.GetString()?.TrimStart('/')))
                    : string.Empty,
                item.TryGetProperty("Image", out var image) ? image.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("State", out var state) ? state.GetString() ?? "unknown" : "unknown",
                item.TryGetProperty("Status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
                ports,
                createdSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(createdSeconds) : null));
        }

        return result;
    }

    public async Task<ContainerLogs> GetLogsAsync(
        string containerId,
        int tail = 100,
        CancellationToken cancellationToken = default)
    {
        if (!ContainerIdPattern.IsMatch(containerId))
        {
            throw new ArgumentException("Container id must be a 12 to 64 character hexadecimal id.", nameof(containerId));
        }

        var boundedTail = Math.Clamp(tail, 1, 1000);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var client = CreateClient();
        using var response = await client.GetAsync(
            $"containers/{containerId}/logs?stdout=1&stderr=1&timestamps=1&tail={boundedTail}",
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadBoundedAsync(stream, 4096, timeout.Token);
            EnsureSuccess(response, error);
        }

        var responseBody = await ReadBoundedBytesAsync(stream, 1024 * 1024, timeout.Token);
        return new ContainerLogs(containerId, boundedTail, DecodeDockerLogStream(responseBody));
    }

    public DockerConnectionStatus GetConnectionStatus()
    {
        var configured = TryGetBaseAddress(out _);
        return new DockerConnectionStatus(configured ? "Configured" : "NotConfigured");
    }

    private HttpClient CreateClient()
    {
        if (!TryGetBaseAddress(out var baseAddress))
        {
            throw new InvalidOperationException("Docker monitoring is not configured.");
        }

        var client = _httpClientFactory.CreateClient(nameof(DockerMonitoringService));
        client.BaseAddress = new Uri(baseAddress!.ToString().TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(15);
        var token = _configuration["Docker:BearerToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private bool TryGetBaseAddress(out Uri? address)
    {
        var configured = _configuration["Docker:ApiBaseUrl"];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out address))
        {
            address = null;
            return false;
        }

        return string.IsNullOrEmpty(address.UserInfo) &&
               string.IsNullOrEmpty(address.Query) &&
               string.IsNullOrEmpty(address.Fragment) &&
               (address.Scheme == Uri.UriSchemeHttps ||
                (_configuration.GetValue<bool>("Docker:AllowInsecureHttp") && address.IsLoopback));
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Docker API returned HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 2048)]}");
        }
    }

    private static async Task<string> ReadBoundedAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken) =>
        Encoding.UTF8.GetString(await ReadBoundedBytesAsync(stream, maxBytes, cancellationToken));

    private static async Task<byte[]> ReadBoundedBytesAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return content.ToArray();
            }

            if (content.Length + read > maxBytes)
            {
                throw new InvalidOperationException(
                    $"Docker API response exceeded the {maxBytes}-byte safety limit.");
            }

            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static string DecodeDockerLogStream(byte[] stream)
    {
        var text = new StringBuilder();
        var offset = 0;
        while (offset + 8 <= stream.Length && stream[offset] is 0 or 1 or 2)
        {
            var frameLength = stream[offset + 4] << 24 |
                             stream[offset + 5] << 16 |
                             stream[offset + 6] << 8 |
                             stream[offset + 7];
            if (frameLength < 0 || offset + 8 + frameLength > stream.Length)
            {
                throw new InvalidOperationException("Docker returned an invalid multiplexed log frame.");
            }

            text.Append(Encoding.UTF8.GetString(stream, offset + 8, frameLength));
            offset += 8 + frameLength;
        }

        return offset == 0 ? Encoding.UTF8.GetString(stream) : text.ToString();
    }
}

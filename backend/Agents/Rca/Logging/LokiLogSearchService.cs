using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace backend.Agents.Rca.Logging;

public sealed class LokiLogSearchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public LokiLogSearchService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyCollection<LogEntry>> SearchAsync(
        LogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuredUrl = _configuration["Logging:LokiBaseUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return Array.Empty<LogEntry>();
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps &&
             !(_configuration.GetValue<bool>("Logging:AllowInsecureHttp") && baseUri.IsLoopback)))
        {
            throw new InvalidOperationException(
                "Logging:LokiBaseUrl must be an absolute HTTPS URL; HTTP is permitted only for loopback development when Logging:AllowInsecureHttp=true.");
        }

        var client = _httpClientFactory.CreateClient(nameof(LokiLogSearchService));
        client.BaseAddress = new Uri(baseUri.ToString().TrimEnd('/') + "/");
        var token = _configuration["Logging:LokiBearerToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        var limit = Math.Clamp(request.MaximumResults, 1, 1000);
        var logQuery = "{}";
        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            logQuery += " |= " + JsonSerializer.Serialize(request.Target);
        }

        var query = string.Join("&",
            "query=" + Uri.EscapeDataString(logQuery),
            "start=" + Uri.EscapeDataString(ToNanoseconds(request.From)),
            "end=" + Uri.EscapeDataString(ToNanoseconds(request.To)),
            "limit=" + limit.ToString(CultureInfo.InvariantCulture),
            "direction=BACKWARD");
        using var response = await client.GetAsync(
            "loki/api/v1/query_range?" + query,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Loki query failed with {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var entries = new List<LogEntry>();
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var results))
        {
            throw new InvalidOperationException("Loki response did not contain data.result.");
        }

        foreach (var stream in results.EnumerateArray())
        {
            var labels = stream.GetProperty("stream");
            var source = GetLabel(labels, "service_name") ??
                         GetLabel(labels, "app") ?? "Loki";
            var target = GetLabel(labels, "target") ?? request.Target;
            foreach (var pair in stream.GetProperty("values").EnumerateArray())
            {
                var timestampText = pair[0].GetString();
                var message = pair[1].GetString() ?? string.Empty;
                if (!long.TryParse(timestampText, CultureInfo.InvariantCulture, out var nanoseconds))
                {
                    throw new InvalidOperationException("Loki returned an invalid log timestamp.");
                }

                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000);
                if (timestamp < request.From || timestamp > request.To)
                {
                    continue;
                }

                var level = GetLabel(labels, "level") ?? "Information";
                var exceptionType = GetLabel(labels, "exception_type");
                var traceId = GetLabel(labels, "trace_id");
                var correlationId = GetLabel(labels, "correlation_id");

                if (message.StartsWith('{'))
                {
                    try
                    {
                        using var lineDocument = JsonDocument.Parse(message);
                        var line = lineDocument.RootElement;
                        message = GetJsonString(line, "message") ?? message;
                        level = GetJsonString(line, "level") ?? level;
                        exceptionType = GetJsonString(line, "exceptionType") ?? exceptionType;
                        traceId = GetJsonString(line, "traceId") ?? traceId;
                        correlationId = GetJsonString(line, "correlationId") ?? correlationId;
                    }
                    catch (JsonException)
                    {
                        // Plain-text log lines are valid Loki values too.
                    }
                }

                if (request.Levels.Count > 0 &&
                    !request.Levels.Contains(level, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries.Add(new LogEntry
                {
                    Timestamp = timestamp,
                    Level = level,
                    Source = source,
                    Message = message,
                    ExceptionType = exceptionType,
                    TraceId = traceId,
                    CorrelationId = correlationId,
                    Target = target
                });
            }
        }

        return entries
            .OrderByDescending(x => x.Timestamp)
            .Take(limit)
            .ToArray();
    }

    private static string ToNanoseconds(DateTimeOffset timestamp) =>
        checked(timestamp.ToUnixTimeMilliseconds() * 1_000_000L)
            .ToString(CultureInfo.InvariantCulture);

    private static string? GetLabel(JsonElement labels, string name) =>
        labels.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static string? GetJsonString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

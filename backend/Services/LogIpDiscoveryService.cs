using System.Net;
using System.Text.RegularExpressions;
using backend.Models;

namespace backend.Services;

public sealed record DiscoveredIp(
    string Ip,
    int Occurrences,
    string[] Containers,
    bool IsPrivate,
    bool AlreadyMonitored);

public sealed record LogScanResult(
    int ContainersScanned,
    int ContainersFailed,
    IReadOnlyList<DiscoveredIp> Addresses);

public sealed class AddDiscoveredIpRequest
{
    public string? Ip { get; init; }
    public string? Scheme { get; init; }
    public string? Name { get; init; }
}

public sealed record AddDiscoveredResult(
    IReadOnlyList<MonitoredEndpoint> Added,
    IReadOnlyList<string> Skipped);

public sealed class LogIpDiscoveryService
{
    public const int MaxContainers = 25;
    public const int MaxAddresses = 200;
    public const int MaxAddBatch = 50;

    private static readonly Regex Ipv4Pattern = new(
        @"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?!\d|\.\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private readonly DockerMonitoringService _docker;
    private readonly EndpointService _endpoints;
    private readonly ILogger<LogIpDiscoveryService> _logger;

    public LogIpDiscoveryService(
        DockerMonitoringService docker,
        EndpointService endpoints,
        ILogger<LogIpDiscoveryService> logger)
    {
        _docker = docker;
        _endpoints = endpoints;
        _logger = logger;
    }

    public static IEnumerable<string> ExtractIps(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match match in Ipv4Pattern.Matches(text))
        {
            if (IsMonitorable(match.Value))
            {
                yield return match.Value;
            }
        }
    }

    // Log content is untrusted, so loopback, link-local (cloud metadata), multicast and reserved ranges are never offered.
    public static bool IsMonitorable(string? value)
    {
        if (!IPAddress.TryParse(value, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] != 0 && b[0] != 127 && b[0] < 224 && !(b[0] == 169 && b[1] == 254);
    }

    public static bool IsPrivate(string ip)
    {
        var b = IPAddress.Parse(ip).GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }

    public async Task<LogScanResult> ScanContainersAsync(int tail, CancellationToken cancellationToken)
    {
        var containers = (await _docker.GetContainersAsync(cancellationToken))
            .Where(c => string.Equals(c.State, "running", StringComparison.OrdinalIgnoreCase))
            .Take(MaxContainers)
            .ToArray();

        var found = new Dictionary<string, (int Count, HashSet<string> Sources)>();
        var failed = 0;

        foreach (var container in containers)
        {
            var name = string.IsNullOrWhiteSpace(container.Names) ? container.Id[..12] : container.Names;
            try
            {
                var logs = await _docker.GetLogsAsync(container.Id, tail, cancellationToken);
                foreach (var ip in ExtractIps(logs.Content))
                {
                    if (!found.TryGetValue(ip, out var entry))
                    {
                        entry = (0, new HashSet<string>());
                    }

                    entry.Sources.Add(name);
                    found[ip] = (entry.Count + 1, entry.Sources);
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                failed++;
                _logger.LogWarning(exception, "Log scan failed for container {Container}.", name);
            }
        }

        var monitoredHosts = MonitoredHosts();
        var addresses = found
            .OrderByDescending(pair => pair.Value.Count)
            .Take(MaxAddresses)
            .Select(pair => new DiscoveredIp(
                pair.Key,
                pair.Value.Count,
                pair.Value.Sources.Order().ToArray(),
                IsPrivate(pair.Key),
                monitoredHosts.Contains(pair.Key)))
            .ToArray();

        return new LogScanResult(containers.Length, failed, addresses);
    }

    public async Task<AddDiscoveredResult> AddAsync(
        IReadOnlyList<AddDiscoveredIpRequest> requests,
        CancellationToken cancellationToken)
    {
        var added = new List<MonitoredEndpoint>();
        var skipped = new List<string>();
        var monitoredHosts = MonitoredHosts();

        foreach (var request in requests.Take(MaxAddBatch))
        {
            var ip = request.Ip?.Trim();
            var scheme = string.IsNullOrWhiteSpace(request.Scheme) ? "http" : request.Scheme.Trim().ToLowerInvariant();

            if (!IsMonitorable(ip) || scheme is not ("http" or "https") || !monitoredHosts.Add(ip!))
            {
                skipped.Add(ip ?? "(empty)");
                continue;
            }

            var name = string.IsNullOrWhiteSpace(request.Name) ? $"Discovered {ip}" : request.Name.Trim();
            added.Add(await _endpoints.CreateAsync(
                new CreateEndpointRequest { Name = name[..Math.Min(name.Length, 100)], Url = $"{scheme}://{ip}" },
                cancellationToken));
        }

        return new AddDiscoveredResult(added, skipped);
    }

    private HashSet<string> MonitoredHosts() => _endpoints.GetAll()
        .Select(e => Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) ? uri.Host : null)
        .OfType<string>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

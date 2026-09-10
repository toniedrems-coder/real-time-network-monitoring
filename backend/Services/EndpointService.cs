using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using backend.Models;

namespace backend.Services;

public sealed class EndpointService
{
    private readonly ConcurrentDictionary<Guid, MonitoredEndpoint> endpoints = new();

    public IReadOnlyList<MonitoredEndpoint> GetAll()
    {
        return endpoints.Values.OrderBy(endpoint => endpoint.Name).ToArray();
    }

    public MonitoredEndpoint? GetById(Guid id)
    {
        return endpoints.TryGetValue(id, out var endpoint) ? endpoint : null;
    }

    public MonitoredEndpoint Create(CreateEndpointRequest request)
    {
        var endpoint = new MonitoredEndpoint(
            Guid.NewGuid(),
            request.Name!.Trim(),
            NormalizeUrl(request.Url!),
            "Unknown",
            DateTimeOffset.UtcNow);

        endpoints[endpoint.Id] = endpoint;
        return endpoint;
    }

    public bool Delete(Guid id)
    {
        return endpoints.TryRemove(id, out _);
    }

    public MonitoredEndpoint? UpdateStatus(Guid id, string status)
    {
        if (!endpoints.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with { Status = status };
        endpoints[id] = updated;
        return updated;
    }

    public MonitoredEndpoint? Update(Guid id, CreateEndpointRequest request)
    {
        if (!endpoints.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with
        {
            Name = request.Name!.Trim(),
            Url = NormalizeUrl(request.Url!)
        };

        endpoints[id] = updated;
        return updated;
    }

    public async Task<IReadOnlyList<MonitoredEndpoint>> DiscoverAsync(
        IEnumerable<string> hostNames,
        CancellationToken cancellationToken = default)
    {
        var discovered = new List<MonitoredEndpoint>();

        foreach (var hostName in hostNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedHost = hostName.Trim();
            if (normalizedHost.Length == 0)
            {
                continue;
            }

            var existing = endpoints.Values.FirstOrDefault(endpoint =>
                Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Host, normalizedHost, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                discovered.Add(existing);
                continue;
            }

            var status = "Discovered";
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(normalizedHost, cancellationToken);
                if (addresses.Length == 0)
                {
                    status = "Unresolved";
                }
            }
            catch (SocketException)
            {
                status = "Unresolved";
            }

            var endpoint = new MonitoredEndpoint(
                Guid.NewGuid(),
                normalizedHost,
                $"https://{normalizedHost}/",
                status,
                DateTimeOffset.UtcNow);

            endpoints[endpoint.Id] = endpoint;
            discovered.Add(endpoint);
        }

        return discovered;
    }

    private static string NormalizeUrl(string url)
    {
        return new Uri(url, UriKind.Absolute).ToString();
    }
}
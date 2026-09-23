using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using backend.Data;
using backend.Models;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Services;

public sealed class EndpointService
{
    private readonly ConcurrentDictionary<Guid, MonitoredEndpoint> endpoints = new();

    // Signals when persisted endpoints have been loaded from PostgreSQL.
    // MonitoringService waits for this before running endpoint discovery.
    private readonly TaskCompletionSource<bool> initializationCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<EndpointService> logger;

    public EndpointService(
        IServiceScopeFactory scopeFactory,
        ILogger<EndpointService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    public IReadOnlyList<MonitoredEndpoint> GetAll()
    {
        return endpoints.Values
            .OrderBy(endpoint => endpoint.Name)
            .ToArray();
    }

    public MonitoredEndpoint? GetById(Guid id)
    {
        return endpoints.TryGetValue(id, out var endpoint)
            ? endpoint
            : null;
    }

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------

    public void Load(IEnumerable<MonitoredEndpoint> persistedEndpoints)
    {
        foreach (var endpoint in persistedEndpoints)
        {
            endpoints[endpoint.Id] = endpoint;
        }
    }

    public Task WaitUntilInitializedAsync(
        CancellationToken cancellationToken = default)
    {
        return initializationCompleted.Task.WaitAsync(cancellationToken);
    }

    public void MarkInitialized()
    {
        initializationCompleted.TrySetResult(true);
    }

    // ---------------------------------------------------------
    // CREATE
    // ---------------------------------------------------------

    public async Task<MonitoredEndpoint> CreateAsync(
        CreateEndpointRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = new MonitoredEndpoint(
            Guid.NewGuid(),
            request.Name!.Trim(),
            NormalizeUrl(request.Url!),
            "Unknown",
            DateTimeOffset.UtcNow);

        using var scope = scopeFactory.CreateScope();

        var repository =
            scope.ServiceProvider.GetRequiredService<IEndpointRepository>();

        await repository.AddAsync(
            endpoint,
            cancellationToken);

        endpoints[endpoint.Id] = endpoint;

        logger.LogInformation(
            "Endpoint {EndpointId} ({EndpointName}) created and persisted.",
            endpoint.Id,
            endpoint.Name);

        return endpoint;
    }

    // ---------------------------------------------------------
    // UPDATE
    // ---------------------------------------------------------

    public async Task<MonitoredEndpoint?> UpdateAsync(
        Guid id,
        CreateEndpointRequest request,
        CancellationToken cancellationToken = default)
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

        using var scope = scopeFactory.CreateScope();

        var repository =
            scope.ServiceProvider.GetRequiredService<IEndpointRepository>();

        await repository.UpdateAsync(
            updated,
            cancellationToken);

        endpoints[id] = updated;

        logger.LogInformation(
            "Endpoint {EndpointId} updated and persisted.",
            id);

        return updated;
    }

    // ---------------------------------------------------------
    // DELETE
    // ---------------------------------------------------------

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!endpoints.ContainsKey(id))
        {
            return false;
        }

        using var scope = scopeFactory.CreateScope();

        var repository =
            scope.ServiceProvider.GetRequiredService<IEndpointRepository>();

        await repository.DeleteAsync(
            id,
            cancellationToken);

        endpoints.TryRemove(id, out _);

        logger.LogInformation(
            "Endpoint {EndpointId} deleted.",
            id);

        return true;
    }

    // ---------------------------------------------------------
    // STATUS
    // ---------------------------------------------------------

public async Task<MonitoredEndpoint?> UpdateStatusAsync(
    Guid id,
    string status,
    CancellationToken cancellationToken = default)
{
    if (!endpoints.TryGetValue(id, out var existing))
    {
        return null;
    }

    // Do nothing when the status has not changed.
    // This prevents a PostgreSQL UPDATE on every probe.
    if (string.Equals(
        existing.Status,
        status,
        StringComparison.OrdinalIgnoreCase))
    {
        return existing;
    }

    var updated = existing with
    {
        Status = status
    };

    using var scope = scopeFactory.CreateScope();

    var repository =
        scope.ServiceProvider
            .GetRequiredService<IEndpointRepository>();

    // Persist first.
    await repository.UpdateAsync(
        updated,
        cancellationToken);

    // Update runtime cache only after PostgreSQL succeeds.
    endpoints[id] = updated;

    logger.LogInformation(
        "Endpoint {EndpointId} status changed from {OldStatus} to {NewStatus}.",
        id,
        existing.Status,
        status);

    return updated;
}


    // ---------------------------------------------------------
    // DISCOVERY
    // ---------------------------------------------------------

    public async Task<IReadOnlyList<MonitoredEndpoint>> DiscoverAsync(
        IEnumerable<string> hostNames,
        CancellationToken cancellationToken = default)
    {
        var discovered = new List<MonitoredEndpoint>();

        foreach (var hostName in
                 hostNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedHost = hostName.Trim();

            if (normalizedHost.Length == 0)
            {
                continue;
            }

            var existing = endpoints.Values.FirstOrDefault(endpoint =>
                Uri.TryCreate(
                    endpoint.Url,
                    UriKind.Absolute,
                    out var uri) &&
                string.Equals(
                    uri.Host,
                    normalizedHost,
                    StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                discovered.Add(existing);
                continue;
            }

            var status = "Discovered";

            try
            {
                var addresses =
                    await Dns.GetHostAddressesAsync(
                        normalizedHost,
                        cancellationToken);

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

            using var scope = scopeFactory.CreateScope();

            var repository =
                scope.ServiceProvider
                    .GetRequiredService<IEndpointRepository>();

            await repository.AddAsync(
                endpoint,
                cancellationToken);

            endpoints[endpoint.Id] = endpoint;

            discovered.Add(endpoint);

            logger.LogInformation(
                "Discovered endpoint {EndpointName} and persisted it.",
                endpoint.Name);
        }

        return discovered;
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------

    private static string NormalizeUrl(string url)
    {
        return new Uri(
            url,
            UriKind.Absolute).ToString();
    }
}

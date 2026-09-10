namespace backend.Models;

public sealed record MonitoredEndpoint(
    Guid Id,
    string Name,
    string Url,
    string Status,
    DateTimeOffset CreatedAt);

public sealed class CreateEndpointRequest
{
    public string? Name { get; init; }
    public string? Url { get; init; }
}

using backend.Models;

namespace backend.Agents.Tools.Monitoring;

public interface IEndpointProbeService
{
    Task<EndpointProbeResult> ProbeAsync(
        MonitoredEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
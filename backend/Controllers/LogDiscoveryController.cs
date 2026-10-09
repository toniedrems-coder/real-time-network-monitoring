using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/log-discovery")]
public sealed class LogDiscoveryController : ControllerBase
{
    private readonly LogIpDiscoveryService _discovery;

    public LogDiscoveryController(LogIpDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    [HttpGet("scan")]
    public async Task<IActionResult> Scan([FromQuery] int tail = 500, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _discovery.ScanContainersAsync(Math.Clamp(tail, 1, 1000), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] List<AddDiscoveredIpRequest> requests, CancellationToken cancellationToken)
    {
        if (requests is null || requests.Count == 0)
        {
            return BadRequest(new { message = "Select at least one address." });
        }

        if (requests.Count > LogIpDiscoveryService.MaxAddBatch)
        {
            return BadRequest(new { message = $"Add at most {LogIpDiscoveryService.MaxAddBatch} addresses at a time." });
        }

        return Ok(await _discovery.AddAsync(requests, cancellationToken));
    }
}

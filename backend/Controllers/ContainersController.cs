using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/containers")]
public sealed class ContainersController : ControllerBase
{
    private readonly DockerMonitoringService _docker;

    public ContainersController(DockerMonitoringService docker)
    {
        _docker = docker;
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(_docker.GetConnectionStatus());

    [HttpGet]
    public async Task<IActionResult> GetContainers(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _docker.GetContainersAsync(cancellationToken));
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

    [HttpGet("{id}/logs")]
    public async Task<IActionResult> GetLogs(
        string id,
        [FromQuery] int tail = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _docker.GetLogsAsync(id, tail, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
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
}

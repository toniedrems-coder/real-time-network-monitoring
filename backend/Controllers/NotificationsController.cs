using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly WorkflowNotificationService _notifications;

    public NotificationsController(WorkflowNotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(_notifications.GetConfigurationStatus());

    [HttpPost("test/{channel}")]
    public async Task<IActionResult> SendTest(
        string channel,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.SendTestAsync(channel, cancellationToken);
            return Ok(new { status = "Sent", channel });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = $"Notification test failed: {exception.Message}" });
        }
    }
}

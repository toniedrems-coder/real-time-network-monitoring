using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Local development OAuth2 client-credentials token endpoint. This mimics the
/// token issuance step of a real identity provider (e.g. Azure AD) so the API's
/// bearer-token authentication and Swagger's Authorize flow can be exercised
/// without connecting to an external IdP. Registered only when the app is
/// running in the Development environment (see Program.cs).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("connect")]
public class DevTokenController : ControllerBase
{
    private readonly DevTokenService _devTokenService;

    public DevTokenController(DevTokenService devTokenService)
    {
        _devTokenService = devTokenService;
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult IssueToken([FromForm] string? grant_type, [FromForm] string? client_id, [FromForm] string? client_secret)
    {
        if (!_devTokenService.ValidateClientCredentials(client_id, client_secret, grant_type))
        {
            return BadRequest(new { error = "invalid_client" });
        }

        var (accessToken, expiresIn) = _devTokenService.IssueToken();
        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = expiresIn
        });
    }
}

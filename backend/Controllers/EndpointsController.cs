using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("endpoints")]
public sealed class EndpointsController : ControllerBase
{
    private readonly EndpointService endpointService;

    public EndpointsController(EndpointService endpointService)
    {
        this.endpointService = endpointService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<MonitoredEndpoint>> GetAll()
    {
        return Ok(endpointService.GetAll());
    }

    [HttpPost]
public async Task<ActionResult<MonitoredEndpoint>> Create(
    CreateEndpointRequest request,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        ModelState.AddModelError(
            nameof(request.Name),
            "Name is required.");
    }

    if (!Uri.TryCreate(
            request.Url,
            UriKind.Absolute,
            out var uri) ||
        uri.Scheme is not ("http" or "https") ||
        string.IsNullOrWhiteSpace(uri.Host))
    {
        ModelState.AddModelError(
            nameof(request.Url),
            "URL must be a valid HTTP or HTTPS URL.");
    }

    if (!ModelState.IsValid)
    {
        return ValidationProblem(ModelState);
    }

    var endpoint =
        await endpointService.CreateAsync(
            request,
            cancellationToken);

    return CreatedAtAction(
        nameof(GetById),
        new { id = endpoint.Id },
        endpoint);
}

    [HttpGet("{id:guid}")]
    public ActionResult<MonitoredEndpoint> GetById(Guid id)
    {
        var endpoint = endpointService.GetById(id);
        return endpoint is not null
            ? Ok(endpoint)
            : NotFound();
    }

[HttpPut("{id:guid}")]
public async Task<ActionResult<MonitoredEndpoint>> Update(
    Guid id,
    CreateEndpointRequest request,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        ModelState.AddModelError(
            nameof(request.Name),
            "Name is required.");
    }

    if (!Uri.TryCreate(
            request.Url,
            UriKind.Absolute,
            out var uri) ||
        uri.Scheme is not ("http" or "https") ||
        string.IsNullOrWhiteSpace(uri.Host))
    {
        ModelState.AddModelError(
            nameof(request.Url),
            "URL must be a valid HTTP or HTTPS URL.");
    }

    if (!ModelState.IsValid)
    {
        return ValidationProblem(ModelState);
    }

    var endpoint =
        await endpointService.UpdateAsync(
            id,
            request,
            cancellationToken);

    return endpoint is not null
        ? Ok(endpoint)
        : NotFound();
}



[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(
    Guid id,
    CancellationToken cancellationToken)
{
    var deleted =
        await endpointService.DeleteAsync(
            id,
            cancellationToken);

    return deleted
        ? NoContent()
        : NotFound();
}

}
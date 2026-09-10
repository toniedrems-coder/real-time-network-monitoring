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
    public ActionResult<MonitoredEndpoint> Create(CreateEndpointRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            ModelState.AddModelError(nameof(request.Url), "URL must be a valid HTTP or HTTPS URL.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var endpoint = endpointService.Create(request);

        return CreatedAtAction(nameof(GetById), new { id = endpoint.Id }, endpoint);
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
    public ActionResult<MonitoredEndpoint> Update(Guid id, CreateEndpointRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            ModelState.AddModelError(nameof(request.Url), "URL must be a valid HTTP or HTTPS URL.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var endpoint = endpointService.Update(id, request);
        return endpoint is not null
            ? Ok(endpoint)
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        return endpointService.Delete(id)
            ? NoContent()
            : NotFound();
    }
}
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly KnowledgeBaseService _knowledgeBaseService;

    public KnowledgeController(
        KnowledgeBaseService knowledgeBaseService)
    {
        _knowledgeBaseService =
            knowledgeBaseService;
    }

    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyCollection<KnowledgeArticle>>>
        GetAll(
            CancellationToken cancellationToken)
    {
        var articles =
            await _knowledgeBaseService.GetAllAsync(
                cancellationToken);

        return Ok(articles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeArticle>>
        GetById(
            Guid id,
            CancellationToken cancellationToken)
    {
        var article =
            await _knowledgeBaseService.GetByIdAsync(
                id,
                cancellationToken);

        if (article is null)
        {
            return NotFound(new
            {
                message =
                    "Knowledge article was not found."
            });
        }

        return Ok(article);
    }

    [HttpGet("number/{articleNumber}")]
    public async Task<ActionResult<KnowledgeArticle>>
        GetByArticleNumber(
            string articleNumber,
            CancellationToken cancellationToken)
    {
        var article =
            await _knowledgeBaseService
                .GetByArticleNumberAsync(
                    articleNumber,
                    cancellationToken);

        if (article is null)
        {
            return NotFound(new
            {
                message =
                    "Knowledge article was not found."
            });
        }

        return Ok(article);
    }

    [HttpGet("search")]
    public async Task<ActionResult<
        IReadOnlyCollection<KnowledgeArticle>>>
        Search(
            [FromQuery] string? target,
            [FromQuery] string? failureType,
            [FromQuery] KnowledgeArticleType? type,
            CancellationToken cancellationToken)
    {
        var articles =
            await _knowledgeBaseService.SearchAsync(
                target,
                failureType,
                type,
                cancellationToken);

        return Ok(articles);
    }

    [HttpPost]
    public async Task<ActionResult<KnowledgeArticle>>
        Create(
            [FromBody]
            CreateKnowledgeArticleRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var article =
                await _knowledgeBaseService.CreateAsync(
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = article.Id },
                article);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeArticle>>
        Update(
            Guid id,
            [FromBody]
            UpdateKnowledgeArticleRequest request,
            CancellationToken cancellationToken)
    {
        var article =
            await _knowledgeBaseService.UpdateAsync(
                id,
                request,
                cancellationToken);

        if (article is null)
        {
            return NotFound(new
            {
                message =
                    "Knowledge article was not found."
            });
        }

        return Ok(article);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult>
        Deactivate(
            Guid id,
            CancellationToken cancellationToken)
    {
        var success =
            await _knowledgeBaseService.DeactivateAsync(
                id,
                cancellationToken);

        if (!success)
        {
            return NotFound(new
            {
                message =
                    "Knowledge article was not found."
            });
        }

        return NoContent();
    }

    [HttpPost("learn/{incidentId:guid}")]
    public async Task<IActionResult> LearnFromIncident(
    Guid incidentId,
    [FromServices] IncidentLearningService learningService,
    CancellationToken cancellationToken)
    {
        var result =
            await learningService
                .LearnFromResolvedIncidentAsync(
                    incidentId,
                    cancellationToken);

        return Ok(result);
    }

}
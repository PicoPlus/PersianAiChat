using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationService _service;

    public ConversationsController(IConversationService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all conversations for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _service.GetUserConversationsAsync(UserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Create a new conversation.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest body, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(UserId, body.Title, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get a conversation by ID (must belong to current user).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, UserId, cancellationToken);
        if (result is null)
            return NotFound(new ProblemDetails { Title = "گفت‌وگو یافت نشد.", Status = 404 });
        return Ok(result);
    }

    /// <summary>Rename a conversation.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameConversationRequest body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return BadRequest(new ProblemDetails { Title = "عنوان نمی‌تواند خالی باشد.", Status = 400 });

        var result = await _service.RenameAsync(id, UserId, body.Title, cancellationToken);
        if (result is null)
            return NotFound(new ProblemDetails { Title = "گفت‌وگو یافت نشد.", Status = 404 });
        return Ok(result);
    }

    /// <summary>Delete a conversation.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, UserId, cancellationToken);
        if (!deleted)
            return NotFound(new ProblemDetails { Title = "گفت‌وگو یافت نشد.", Status = 404 });
        return NoContent();
    }
}

public sealed record CreateConversationRequest(string? Title = null);
public sealed record RenameConversationRequest(string Title);

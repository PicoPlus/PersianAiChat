using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(ITicketService ticketService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a ticket:
    ///   1. GapGPT analyzes the conversation (no web_search — ~600 tokens)
    ///   2. HubSpot Note + Deal created (0 tokens)
    ///   Returns the analysis summary and tracking IDs.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTicketApiRequest body,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (string.IsNullOrWhiteSpace(body.UserQuestion))
            return BadRequest(new ProblemDetails
            {
                Title  = "موضوع درخواست خالی است.",
                Status = 400
            });

        try
        {
            var result = await _ticketService.SubmitAsync(new SubmitTicketRequest(
                UserId:         userId,
                ConversationId: body.ConversationId ?? Guid.Empty,
                UserQuestion:   body.UserQuestion,
                BotAnswer:      body.BotAnswer ?? string.Empty,
                Category:       body.Category  ?? "general"
            ), ct);

            if (!result.Success)
                return StatusCode(500, new ProblemDetails
                {
                    Title  = result.Message,
                    Status = 500
                });

            return Ok(new
            {
                success         = true,
                dealId          = result.DealId,
                noteId          = result.NoteId,
                message         = result.Message,
                analysisSummary = result.AnalysisSummary
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Ticket submission service error");
            return StatusCode(503, new ProblemDetails
            {
                Title  = "سرویس تحلیل در حال حاضر در دسترس نیست. لطفاً کمی بعد تلاش کنید.",
                Status = 503
            });
        }
    }
}

public sealed record CreateTicketApiRequest(
    string  UserQuestion,
    string? BotAnswer,
    string? Category,
    Guid?   ConversationId
);

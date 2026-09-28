using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>Send a message and receive an AI response.</summary>
    [HttpPost]
    [EnableRateLimiting("ChatPolicy")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest body, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (string.IsNullOrWhiteSpace(body.Message))
            return BadRequest(new ProblemDetails
            {
                Title = "پیام نمی‌تواند خالی باشد.",
                Status = 400
            });

        try
        {
            var result = await _chatService.SendMessageAsync(userId, body.ConversationId, body.Message, cancellationToken);
            return Ok(new
            {
                success = result.Success,
                conversationId = result.ConversationId,
                message = new
                {
                    id = result.AssistantMessage.Id,
                    role = result.AssistantMessage.Role,
                    content = result.AssistantMessage.Content,
                    citations = result.AssistantMessage.Citations
                },
                usage = new
                {
                    promptTokens = result.Usage.PromptTokens,
                    completionTokens = result.Usage.CompletionTokens,
                    reasoningTokens = result.Usage.ReasoningTokens,
                    totalTokens = result.Usage.TotalTokens,
                    cost = result.Usage.Cost,
                    webSearchRequests = result.Usage.WebSearchRequests
                },
                model = result.Model,
                requestId = result.RequestId,
                // Ticket CTA fields — null when responseType == "normal"
                responseType = result.ResponseType.ToString().ToLowerInvariant(),
                ticketContext = result.TicketContext is null ? null : new
                {
                    suggestedSubject  = result.TicketContext.SuggestedSubject,
                    suggestedCategory = result.TicketContext.SuggestedCategory,
                    botAnswer         = result.TicketContext.BotAnswer
                }
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Chat service error: {Message}", ex.Message);
            return StatusCode(503, new ProblemDetails
            {
                Title = "سرویس هوش مصنوعی در حال حاضر در دسترس نیست.",
                Status = 503
            });
        }
    }
}

public sealed record ChatRequest(string Message, Guid? ConversationId = null);

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Domain.Entities;
using PersianAiChat.Domain.Enums;

namespace PersianAiChat.Application.Services;

public sealed class ChatService : IChatService
{
    private readonly IApplicationDbContext _db;
    private readonly IGapGptService _gapGpt;
    private readonly IUsageService _usageService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IApplicationDbContext db,
        IGapGptService gapGpt,
        IUsageService usageService,
        ILogger<ChatService> logger)
    {
        _db = db;
        _gapGpt = gapGpt;
        _usageService = usageService;
        _logger = logger;
    }

    public async Task<ChatResponse> SendMessageAsync(Guid userId, Guid? conversationId, string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("پیام نمی‌تواند خالی باشد.");

        // Resolve or create conversation
        Conversation conversation;
        if (conversationId.HasValue)
        {
            conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId.Value && c.UserId == userId, cancellationToken)
                ?? throw new UnauthorizedAccessException("گفت‌وگو یافت نشد یا دسترسی مجاز نیست.");
        }
        else
        {
            var title = userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage;
            conversation = new Conversation { UserId = userId, Title = title };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Save user message
        var userMsg = new Message
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Content = userMessage
        };
        _db.Messages.Add(userMsg);
        await _db.SaveChangesAsync(cancellationToken);

        // Build history for GapGPT
        var history = await _db.Messages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessage(m.Role.ToString().ToLowerInvariant(), m.Content))
            .ToListAsync(cancellationToken);

        // Call GapGPT
        var response = await _gapGpt.CompleteChatAsync(history, cancellationToken);

        _logger.LogInformation(
            "GapGPT response. RequestId={RequestId} Model={Model} TotalTokens={Tokens} WebSearch={WebSearch}",
            response.RequestId, response.ReturnedModel, response.Usage.TotalTokens, response.Usage.WebSearchRequests);

        // Save assistant message
        var assistantMsg = new Message
        {
            ConversationId = conversation.Id,
            Role = MessageRole.Assistant,
            Content = response.Answer,
            RequestId = response.RequestId,
            Model = response.ReturnedModel,
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            ReasoningTokens = response.Usage.ReasoningTokens,
            TotalTokens = response.Usage.TotalTokens,
            Cost = response.Usage.Cost
        };
        _db.Messages.Add(assistantMsg);

        // Update conversation timestamp
        conversation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Record usage
        await _usageService.RecordUsageAsync(userId, conversation.Id, assistantMsg.Id, response.Usage, cancellationToken);

        var citations = response.Citations
            .Select(c => new CitationDto(c.Url, c.Title))
            .ToList();

        // Zero-token intent check — decide whether to offer ticket CTA
        var offerTicket = TicketIntentDetector.ShouldOfferTicket(userMessage, response.Answer);
        var responseType = offerTicket ? ChatResponseType.OfferTicket : ChatResponseType.Normal;
        var ticketContext = offerTicket ? new TicketContext(
            SuggestedSubject:  userMessage.Length > 100 ? userMessage[..100] : userMessage,
            SuggestedCategory: TicketIntentDetector.DetectCategory(userMessage),
            BotAnswer:         response.Answer.Length > 1500 ? response.Answer[..1500] : response.Answer
        ) : null;

        return new ChatResponse(
            true,
            conversation.Id,
            new MessageDto(assistantMsg.Id, "assistant", response.Answer, assistantMsg.CreatedAt, citations),
            new ChatUsageDto(
                response.Usage.PromptTokens,
                response.Usage.CompletionTokens,
                response.Usage.ReasoningTokens,
                response.Usage.TotalTokens,
                response.Usage.Cost,
                response.Usage.WebSearchRequests),
            response.ReturnedModel,
            response.RequestId,
            responseType,
            ticketContext
        );
    }
}

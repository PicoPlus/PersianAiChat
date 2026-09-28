namespace PersianAiChat.Application.Abstractions;

/// <summary>Manages chat message processing for a conversation.</summary>
public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(Guid userId, Guid? conversationId, string userMessage, CancellationToken cancellationToken = default);
}

public enum ChatResponseType
{
    /// <summary>Normal AI answer — no ticket offer.</summary>
    Normal,
    /// <summary>AI answered + keywords detected → offer to submit ticket.</summary>
    OfferTicket
}

/// <summary>Pre-filled context passed to the ticket CTA card in the UI.</summary>
public sealed record TicketContext(
    string SuggestedSubject,
    string SuggestedCategory,
    string BotAnswer            // sent back to /api/tickets for analysis context
);

public sealed record ChatUsageDto(
    int PromptTokens,
    int CompletionTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal Cost,
    int WebSearchRequests
);

public sealed record ChatResponse(
    bool Success,
    Guid ConversationId,
    MessageDto AssistantMessage,
    ChatUsageDto Usage,
    string? Model,
    string? RequestId,
    ChatResponseType ResponseType,
    TicketContext? TicketContext
);

using PersianAiChat.Domain.Common;

namespace PersianAiChat.Domain.Entities;

/// <summary>Detailed token usage record per message/response.</summary>
public sealed class UsageRecord : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int ReasoningTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal Cost { get; set; }
    public int WebSearchRequests { get; set; }

    public User User { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public Message Message { get; set; } = null!;
}

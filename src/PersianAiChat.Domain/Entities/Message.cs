using PersianAiChat.Domain.Common;
using PersianAiChat.Domain.Enums;

namespace PersianAiChat.Domain.Entities;

/// <summary>A single chat message (user or assistant) in a conversation.</summary>
public sealed class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>The upstream request/correlation ID returned by GapGPT.</summary>
    public string? RequestId { get; set; }

    /// <summary>Model name as returned by GapGPT (e.g., "openai/gpt-6-luna").</summary>
    public string? Model { get; set; }

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int ReasoningTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal Cost { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public UsageRecord? UsageRecord { get; set; }
}

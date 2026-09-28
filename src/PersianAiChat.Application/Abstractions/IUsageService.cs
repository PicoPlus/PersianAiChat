namespace PersianAiChat.Application.Abstractions;

/// <summary>Cumulative usage summary for a user.</summary>
public sealed record UsageSummaryDto(
    long TotalTokens,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    long TotalReasoningTokens,
    decimal TotalCost,
    int TotalWebSearchRequests
);

/// <summary>Retrieves aggregated token usage statistics.</summary>
public interface IUsageService
{
    Task<UsageSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordUsageAsync(Guid userId, Guid conversationId, Guid messageId, GapGptUsage usage, CancellationToken cancellationToken = default);
}

using Microsoft.EntityFrameworkCore;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Domain.Entities;

namespace PersianAiChat.Application.Services;

public sealed class UsageService : IUsageService
{
    private readonly IApplicationDbContext _db;

    public UsageService(IApplicationDbContext db) => _db = db;

    public async Task<UsageSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _db.UsageRecords
            .Where(r => r.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTokens = (long)g.Sum(r => r.TotalTokens),
                TotalPromptTokens = (long)g.Sum(r => r.PromptTokens),
                TotalCompletionTokens = (long)g.Sum(r => r.CompletionTokens),
                TotalReasoningTokens = (long)g.Sum(r => r.ReasoningTokens),
                TotalCost = g.Sum(r => r.Cost),
                TotalWebSearch = g.Sum(r => r.WebSearchRequests)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            return new UsageSummaryDto(0, 0, 0, 0, 0m, 0);

        return new UsageSummaryDto(
            result.TotalTokens,
            result.TotalPromptTokens,
            result.TotalCompletionTokens,
            result.TotalReasoningTokens,
            result.TotalCost,
            result.TotalWebSearch
        );
    }

    public async Task RecordUsageAsync(Guid userId, Guid conversationId, Guid messageId, GapGptUsage usage, CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            UserId = userId,
            ConversationId = conversationId,
            MessageId = messageId,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            ReasoningTokens = usage.ReasoningTokens,
            TotalTokens = usage.TotalTokens,
            Cost = usage.Cost,
            WebSearchRequests = usage.WebSearchRequests
        };
        _db.UsageRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

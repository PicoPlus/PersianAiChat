using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersianAiChat.Application.Services;
using PersianAiChat.Infrastructure.Persistence;

namespace PersianAiChat.UnitTests;

public class UsageServiceTests
{
    private static ApplicationDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task GetSummary_NoRecords_ReturnsZero()
    {
        var db = CreateDb();
        var svc = new UsageService(db);
        var summary = await svc.GetSummaryAsync(Guid.NewGuid());
        summary.TotalTokens.Should().Be(0);
        summary.TotalCost.Should().Be(0m);
    }

    [Fact]
    public async Task RecordUsage_ThenGetSummary_ReturnsAggregated()
    {
        var db = CreateDb();
        var svc = new UsageService(db);
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        // Need User, Conversation, Message in DB for FK constraints (in-memory ignores them)
        var usage1 = new PersianAiChat.Application.Abstractions.GapGptUsage(1000, 200, 100, 1200, 0.01m, 2);
        var usage2 = new PersianAiChat.Application.Abstractions.GapGptUsage(500, 100, 50, 600, 0.005m, 1);

        await svc.RecordUsageAsync(userId, convId, Guid.NewGuid(), usage1);
        await svc.RecordUsageAsync(userId, convId, Guid.NewGuid(), usage2);

        var summary = await svc.GetSummaryAsync(userId);
        summary.TotalTokens.Should().Be(1800);
        summary.TotalPromptTokens.Should().Be(1500);
        summary.TotalCompletionTokens.Should().Be(300);
        summary.TotalReasoningTokens.Should().Be(150);
        summary.TotalCost.Should().Be(0.015m);
        summary.TotalWebSearchRequests.Should().Be(3);
    }

    [Fact]
    public async Task GetSummary_DoesNotIncludeOtherUsersData()
    {
        var db = CreateDb();
        var svc = new UsageService(db);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        await svc.RecordUsageAsync(userId1, Guid.NewGuid(), Guid.NewGuid(),
            new PersianAiChat.Application.Abstractions.GapGptUsage(100, 50, 0, 150, 0.001m, 0));

        var summary2 = await svc.GetSummaryAsync(userId2);
        summary2.TotalTokens.Should().Be(0);
    }
}

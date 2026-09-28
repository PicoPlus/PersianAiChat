using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Services;
using PersianAiChat.Domain.Entities;
using PersianAiChat.Domain.Enums;
using PersianAiChat.Infrastructure.Persistence;

namespace PersianAiChat.UnitTests;

public class ConversationOwnershipTests
{
    private static ApplicationDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task GetById_OtherUsersConversation_ReturnsNull()
    {
        var db = CreateDb();
        var user1 = new User { Phone = "989111111111", HubSpotContactId = "1" };
        var user2 = new User { Phone = "989222222222", HubSpotContactId = "2" };
        db.Users.AddRange(user1, user2);

        var conv = new Conversation { UserId = user1.Id, Title = "گفت‌وگوی کاربر ۱" };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var svc = new ConversationService(db, NullLogger<ConversationService>.Instance);

        // user1 can access
        var result1 = await svc.GetByIdAsync(conv.Id, user1.Id);
        result1.Should().NotBeNull();

        // user2 cannot access
        var result2 = await svc.GetByIdAsync(conv.Id, user2.Id);
        result2.Should().BeNull();
    }

    [Fact]
    public async Task Delete_OtherUsersConversation_ReturnsFalse()
    {
        var db = CreateDb();
        var user1 = new User { Phone = "989111111111", HubSpotContactId = "1" };
        var user2 = new User { Phone = "989222222222", HubSpotContactId = "2" };
        db.Users.AddRange(user1, user2);
        var conv = new Conversation { UserId = user1.Id, Title = "Test" };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var svc = new ConversationService(db, NullLogger<ConversationService>.Instance);
        var deleted = await svc.DeleteAsync(conv.Id, user2.Id);
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task Rename_OtherUsersConversation_ReturnsNull()
    {
        var db = CreateDb();
        var user1 = new User { Phone = "989111111111", HubSpotContactId = "1" };
        var user2 = new User { Phone = "989222222222", HubSpotContactId = "2" };
        db.Users.AddRange(user1, user2);
        var conv = new Conversation { UserId = user1.Id, Title = "Original" };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var svc = new ConversationService(db, NullLogger<ConversationService>.Instance);
        var result = await svc.RenameAsync(conv.Id, user2.Id, "Hacked");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserConversations_OnlyReturnsOwnConversations()
    {
        var db = CreateDb();
        var user1 = new User { Phone = "989111111111", HubSpotContactId = "1" };
        var user2 = new User { Phone = "989222222222", HubSpotContactId = "2" };
        db.Users.AddRange(user1, user2);
        db.Conversations.Add(new Conversation { UserId = user1.Id, Title = "Conv A" });
        db.Conversations.Add(new Conversation { UserId = user1.Id, Title = "Conv B" });
        db.Conversations.Add(new Conversation { UserId = user2.Id, Title = "Conv C" });
        await db.SaveChangesAsync();

        var svc = new ConversationService(db, NullLogger<ConversationService>.Instance);
        var user1Convs = await svc.GetUserConversationsAsync(user1.Id);
        user1Convs.Should().HaveCount(2);
        user1Convs.Should().AllSatisfy(c => c.Title.Should().BeOneOf("Conv A", "Conv B"));
    }
}

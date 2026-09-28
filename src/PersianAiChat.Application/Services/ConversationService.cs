using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Domain.Entities;
using PersianAiChat.Domain.Enums;

namespace PersianAiChat.Application.Services;

public sealed class ConversationService : IConversationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IApplicationDbContext db, ILogger<ConversationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ConversationDto> CreateAsync(Guid userId, string? initialTitle, CancellationToken cancellationToken = default)
    {
        var title = TruncateTitle(initialTitle ?? "گفت‌وگوی جدید");
        var conv = new Conversation { UserId = userId, Title = title };
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Conversation {ConvId} created for user {UserId}", conv.Id, userId);
        return Map(conv);
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetUserConversationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Conversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationSummaryDto(
                c.Id,
                c.Title,
                c.CreatedAt,
                c.UpdatedAt,
                c.Messages.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationDetailDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);

        if (conv is null) return null;

        var messages = conv.Messages.Select(m => new MessageDto(
            m.Id,
            m.Role.ToString().ToLowerInvariant(),
            m.Content,
            m.CreatedAt
        )).ToList();

        return new ConversationDetailDto(conv.Id, conv.Title, conv.CreatedAt, messages);
    }

    public async Task<ConversationDto?> RenameAsync(Guid id, Guid userId, string newTitle, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (conv is null) return null;

        conv.Title = TruncateTitle(newTitle);
        conv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(conv);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (conv is null) return false;

        _db.Conversations.Remove(conv);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ConversationDto Map(Conversation c) =>
        new(c.Id, c.Title, c.CreatedAt, c.UpdatedAt);

    private static string TruncateTitle(string title)
    {
        const int max = 100;
        if (title.Length <= max) return title;
        return title[..max] + "...";
    }
}

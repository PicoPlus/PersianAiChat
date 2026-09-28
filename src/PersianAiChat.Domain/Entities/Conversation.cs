using PersianAiChat.Domain.Common;

namespace PersianAiChat.Domain.Entities;

/// <summary>A chat conversation belonging to a user.</summary>
public sealed class Conversation : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public ICollection<Message> Messages { get; init; } = new List<Message>();
    public ICollection<UsageRecord> UsageRecords { get; init; } = new List<UsageRecord>();
}

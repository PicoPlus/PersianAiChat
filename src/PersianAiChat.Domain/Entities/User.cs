using PersianAiChat.Domain.Common;

namespace PersianAiChat.Domain.Entities;

/// <summary>Represents an authenticated user, looked up from HubSpot.</summary>
public sealed class User : BaseEntity
{
    public string Phone { get; set; } = string.Empty;        // canonical: 989xxxxxxxxx
    public string HubSpotContactId { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Conversation> Conversations { get; init; } = new List<Conversation>();
    public ICollection<UsageRecord> UsageRecords { get; init; } = new List<UsageRecord>();
}

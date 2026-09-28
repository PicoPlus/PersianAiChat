using PersianAiChat.Domain.Common;

namespace PersianAiChat.Domain.Entities;

/// <summary>
/// Represents a one-time password request.
/// The OTP value is NEVER stored in plaintext — only its SHA-256 hash.
/// </summary>
public sealed class OtpRequest : BaseEntity
{
    /// <summary>Canonical phone number (989xxxxxxxxx).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the OTP code.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? UsedAt { get; set; }

    /// <summary>SHA-256 hash of the requester IP address for rate-limiting.</summary>
    public string? RequestIpHash { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed => UsedAt.HasValue;
    public bool IsValid => !IsExpired && !IsUsed && Attempts < MaxAttempts;
}

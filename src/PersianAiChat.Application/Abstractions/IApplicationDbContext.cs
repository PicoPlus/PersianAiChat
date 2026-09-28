using PersianAiChat.Domain.Entities;

namespace PersianAiChat.Application.Abstractions;

/// <summary>Provides access to the persistence store.</summary>
public interface IApplicationDbContext
{
    Microsoft.EntityFrameworkCore.DbSet<User> Users { get; }
    Microsoft.EntityFrameworkCore.DbSet<Conversation> Conversations { get; }
    Microsoft.EntityFrameworkCore.DbSet<Message> Messages { get; }
    Microsoft.EntityFrameworkCore.DbSet<UsageRecord> UsageRecords { get; }
    Microsoft.EntityFrameworkCore.DbSet<OtpRequest> OtpRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

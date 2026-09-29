using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Domain.Entities;

namespace PersianAiChat.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Phone).IsUnique();
            e.HasIndex(u => u.HubSpotContactId);
            e.Property(u => u.Phone).IsRequired().HasMaxLength(20);
            e.Property(u => u.HubSpotContactId).IsRequired().HasMaxLength(50);
        });

        // Conversation
        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.UserId);
            e.Property(c => c.Title).IsRequired().HasMaxLength(200);
            e.HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Message
        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.ConversationId);
            e.Property(m => m.Content).IsRequired();
            e.Property(m => m.Role).IsRequired();
            e.Property(m => m.Cost).HasColumnType("decimal(18,8)");
            e.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UsageRecord
        modelBuilder.Entity<UsageRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.UserId);
            e.HasIndex(r => r.ConversationId);
            e.HasIndex(r => r.MessageId).IsUnique();
            e.Property(r => r.Cost).HasColumnType("decimal(18,8)");
            e.HasOne(r => r.User)
                .WithMany(u => u.UsageRecords)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Conversation)
                .WithMany(c => c.UsageRecords)
                .HasForeignKey(r => r.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Message)
                .WithOne(m => m.UsageRecord)
                .HasForeignKey<UsageRecord>(r => r.MessageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OtpRequest
        modelBuilder.Entity<OtpRequest>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.Phone);
            e.HasIndex(o => new { o.Phone, o.CreatedAt });
            e.Property(o => o.Phone).IsRequired().HasMaxLength(20);
            e.Property(o => o.CodeHash).IsRequired().HasMaxLength(128);
        });
    }
}

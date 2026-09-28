namespace PersianAiChat.Application.Abstractions;

/// <summary>Manages conversations for authenticated users.</summary>
public interface IConversationService
{
    Task<ConversationDto> CreateAsync(Guid userId, string? initialTitle, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSummaryDto>> GetUserConversationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationDetailDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationDto?> RenameAsync(Guid id, Guid userId, string newTitle, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record ConversationDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ConversationSummaryDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, int MessageCount);

public sealed record MessageDto(Guid Id, string Role, string Content, DateTime CreatedAt, IReadOnlyList<CitationDto>? Citations = null);

public sealed record CitationDto(string Url, string Title);

public sealed record ConversationDetailDto(Guid Id, string Title, DateTime CreatedAt, IReadOnlyList<MessageDto> Messages);

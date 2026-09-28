namespace PersianAiChat.Application.Abstractions;

public sealed record SubmitTicketRequest(
    Guid UserId,
    Guid ConversationId,
    string UserQuestion,       // original user message
    string BotAnswer,          // GapGPT answer shown to user (used as context)
    string Category            // "university" | "welfare" | "general"
);

public sealed record SubmitTicketResponse(
    bool Success,
    string? DealId,
    string? NoteId,
    string Message,
    string? AnalysisSummary    // what GapGPT produced — shown in UI confirmation
);

/// <summary>
/// Orchestrates ticket submission:
///   1. GapGPT.AnalyzeAsync (no web_search, ~600 tokens)
///   2. HubSpotService.CreateTicketAsync (Note + Deal, 0 tokens)
/// </summary>
public interface ITicketService
{
    Task<SubmitTicketResponse> SubmitAsync(
        SubmitTicketRequest request,
        CancellationToken cancellationToken = default);
}

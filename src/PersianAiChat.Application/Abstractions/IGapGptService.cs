namespace PersianAiChat.Application.Abstractions;

/// <summary>Normalized GapGPT usage data.</summary>
public sealed record GapGptUsage(
    int PromptTokens,
    int CompletionTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal Cost,
    int WebSearchRequests
);

/// <summary>A URL citation from web search results.</summary>
public sealed record GapGptCitation(
    string Url,
    string Title,
    int StartIndex,
    int EndIndex
);

/// <summary>Normalized response from GapGPT, safe to pass to the application layer.</summary>
public sealed record GapGptResponse(
    string? RequestId,
    string? RequestedModel,
    string? ReturnedModel,
    string? Provider,
    string Answer,
    string? FinishReason,
    string? NativeFinishReason,
    IReadOnlyList<GapGptCitation> Citations,
    GapGptUsage Usage
);

/// <summary>A single chat message in the conversation history.</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>Interacts with the GapGPT AI completion API.</summary>
public interface IGapGptService
{
    /// <summary>
    /// Full chat completion with web_search tool enabled.
    /// Use for user-facing answers.
    /// </summary>
    Task<GapGptResponse> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Focused analysis WITHOUT the web_search tool.
    /// ~98% fewer prompt tokens — use for internal processing (ticket analysis, summarization).
    /// </summary>
    Task<GapGptResponse> AnalyzeAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default
    );
}

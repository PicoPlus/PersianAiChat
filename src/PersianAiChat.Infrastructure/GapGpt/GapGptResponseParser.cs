using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.Infrastructure.GapGpt;

/// <summary>
/// Converts the raw GapGPT upstream response into the normalized application model.
/// CRITICAL: reasoning and reasoning_details are NEVER included in the output.
/// </summary>
internal static class GapGptResponseParser
{
    internal static GapGptResponse Parse(GapGptRawResponse raw, string requestedModel)
    {
        var choice = raw.Choices?.FirstOrDefault();
        var message = choice?.Message;

        // Extract answer — exclude reasoning entirely
        var answer = message?.Content ?? string.Empty;

        // Extract citations from annotations — url_citation type only
        var citations = new List<GapGptCitation>();
        if (message?.Annotations is { Count: > 0 })
        {
            foreach (var annotation in message.Annotations)
            {
                if (annotation.Type == "url_citation" && annotation.UrlCitation is not null)
                {
                    var cit = annotation.UrlCitation;
                    if (!string.IsNullOrWhiteSpace(cit.Url))
                    {
                        citations.Add(new GapGptCitation(
                            cit.Url,
                            cit.Title ?? cit.Url,
                            cit.StartIndex,
                            cit.EndIndex));
                    }
                }
            }
        }

        // Extract usage
        var rawUsage = raw.Usage;
        var reasoningTokens = rawUsage?.CompletionTokensDetails?.ReasoningTokens ?? 0;
        var webSearchRequests = rawUsage?.ServerToolUseDetails?.WebSearchRequests ?? 0;

        var usage = new GapGptUsage(
            rawUsage?.PromptTokens ?? 0,
            rawUsage?.CompletionTokens ?? 0,
            reasoningTokens,
            rawUsage?.TotalTokens ?? 0,
            rawUsage?.Cost ?? 0m,
            webSearchRequests
        );

        return new GapGptResponse(
            RequestId: raw.Id,
            RequestedModel: requestedModel,
            ReturnedModel: raw.Model,
            Provider: raw.Provider,
            Answer: answer,
            FinishReason: choice?.FinishReason,
            NativeFinishReason: choice?.NativeFinishReason,
            Citations: citations.AsReadOnly(),
            Usage: usage
        );
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersianAiChat.Infrastructure.GapGpt;

// ──────────────────────────────────────────────────────────────────
// Raw upstream DTOs — NEVER exposed beyond Infrastructure
// ──────────────────────────────────────────────────────────────────

internal sealed class GapGptRawRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<GapGptRawMessage> Messages { get; init; } = [];

    [JsonPropertyName("tools")]
    public List<GapGptTool>? Tools { get; init; }
}

internal sealed class GapGptRawMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal sealed class GapGptTool
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

// ──────── Response ────────

internal sealed class GapGptRawResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("created")]
    public long? Created { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; init; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; init; }

    [JsonPropertyName("choices")]
    public List<GapGptChoice>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public GapGptRawUsage? Usage { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed class GapGptChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("native_finish_reason")]
    public string? NativeFinishReason { get; init; }

    [JsonPropertyName("message")]
    public GapGptRawMessageContent? Message { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed class GapGptRawMessageContent
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("refusal")]
    public string? Refusal { get; init; }

    /// <summary>
    /// CRITICAL: reasoning is parsed but NEVER returned to the application layer.
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; init; }

    /// <summary>CRITICAL: reasoning_details are parsed but NEVER returned to the application layer.
    /// Can be a JSON array OR a plain string — captured as raw JsonElement to tolerate both.</summary>
    [JsonPropertyName("reasoning_details")]
    public JsonElement? ReasoningDetails { get; init; }

    [JsonPropertyName("annotations")]
    public List<GapGptAnnotation>? Annotations { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed class GapGptAnnotation
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("url_citation")]
    public GapGptUrlCitation? UrlCitation { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed class GapGptUrlCitation
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("start_index")]
    public int StartIndex { get; init; }

    [JsonPropertyName("end_index")]
    public int EndIndex { get; init; }
}

internal sealed class GapGptRawUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("cost")]
    public decimal Cost { get; init; }

    [JsonPropertyName("is_byok")]
    public bool IsByok { get; init; }

    [JsonPropertyName("prompt_tokens_details")]
    public GapGptPromptTokensDetails? PromptTokensDetails { get; init; }

    [JsonPropertyName("completion_tokens_details")]
    public GapGptCompletionTokensDetails? CompletionTokensDetails { get; init; }

    [JsonPropertyName("cost_details")]
    public GapGptCostDetails? CostDetails { get; init; }

    [JsonPropertyName("server_tool_use_details")]
    public GapGptServerToolUseDetails? ServerToolUseDetails { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed class GapGptPromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; init; }

    [JsonPropertyName("cache_write_tokens")]
    public int CacheWriteTokens { get; init; }

    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; init; }

    [JsonPropertyName("video_tokens")]
    public int VideoTokens { get; init; }
}

internal sealed class GapGptCompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }

    [JsonPropertyName("image_tokens")]
    public int ImageTokens { get; init; }

    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; init; }
}

internal sealed class GapGptCostDetails
{
    [JsonPropertyName("upstream_inference_cost")]
    public decimal UpstreamInferenceCost { get; init; }

    [JsonPropertyName("upstream_inference_prompt_cost")]
    public decimal UpstreamInferencePromptCost { get; init; }

    [JsonPropertyName("upstream_inference_completions_cost")]
    public decimal UpstreamInferenceCompletionsCost { get; init; }
}

internal sealed class GapGptServerToolUseDetails
{
    [JsonPropertyName("web_search_requests")]
    public int WebSearchRequests { get; init; }
}

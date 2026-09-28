using System.Text.Json;
using FluentAssertions;
using PersianAiChat.Infrastructure.GapGpt;

namespace PersianAiChat.UnitTests;

/// <summary>
/// Tests against the exact production GapGPT response shape.
/// Proves reasoning/encrypted data is NEVER surfaced to the application layer.
/// </summary>
public class GapGptResponseParserTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // ── Load the canonical fixture (mirrors actual production response) ────
    private static GapGptRawResponse LoadFixture()
    {
        // Probe several candidate paths (unit test output layout varies)
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "gapgpt-response.json"),
            Path.Combine(AppContext.BaseDirectory, "gapgpt-response.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Fixtures", "gapgpt-response.json"),
        };
        var path = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Test fixture gapgpt-response.json not found.");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GapGptRawResponse>(json, JsonOpts)!;
    }

    // ── Inline replica of actual production response ───────────────────────
    private static GapGptRawResponse BuildActualResponse() =>
        JsonSerializer.Deserialize<GapGptRawResponse>(ActualResponseJson, JsonOpts)!;

    // Exact shape from the production API (reasoning encrypted data truncated for fixture safety)
    private const string ActualResponseJson = """
        {
          "id": "gen-1790594330-iPBrjQ679xUIWCRa3xaT",
          "object": "chat.completion",
          "created": 1790594330,
          "model": "openai/gpt-6-luna",
          "provider": "OpenAI",
          "system_fingerprint": null,
          "service_tier": "default",
          "choices": [
            {
              "index": 0,
              "logprobs": null,
              "finish_reason": "stop",
              "native_finish_reason": "completed",
              "message": {
                "role": "assistant",
                "content": "اگر منظورت **کارشناسی پیوسته** است، انتخاب رشته‌اش هنوز آغاز نشده بود.",
                "refusal": null,
                "reasoning": "**Searching university admissions**\n\nI need to provide the answer in Persian.",
                "reasoning_details": [
                  {
                    "type": "reasoning.summary",
                    "summary": "**Searching university admissions**\n\nSearching for official Azad admissions.",
                    "format": "openai-responses-v1",
                    "index": 0
                  },
                  {
                    "type": "reasoning.encrypted",
                    "data": "gAAAAABquk0datRR_3W67awk_ENCRYPTED_DO_NOT_EXPOSE",
                    "format": "openai-responses-v1",
                    "id": "rs_0c327c3c76b1ee59016aba4d1b63a887d19acd5db54b1a97a7",
                    "index": 1
                  },
                  {
                    "type": "reasoning.summary",
                    "summary": "**Checking registration deadlines**\n\nVerifying if deadline has passed.",
                    "format": "openai-responses-v1",
                    "index": 2
                  },
                  {
                    "type": "reasoning.encrypted",
                    "data": "gAAAAABquk0gkIE5tzOy_ENCRYPTED_DO_NOT_EXPOSE",
                    "format": "openai-responses-v1",
                    "id": "rs_0c327c3c76b1ee59016aba4d1ea8e487d182bc962dcb0919e6",
                    "index": 3
                  }
                ],
                "annotations": [
                  {
                    "type": "url_citation",
                    "url_citation": {
                      "url": "https://tg.me/azaadnews/7252?tr=en&utm_source=openai",
                      "title": "قابل توجه دانشجویان ورودی ۱۴۰۵ | آخرین جزئیات — Azaad news — TG.ME",
                      "start_index": 208,
                      "end_index": 271
                    }
                  }
                ]
              }
            }
          ],
          "usage": {
            "prompt_tokens": 29866,
            "completion_tokens": 1466,
            "total_tokens": 31332,
            "cost": 0.04332675,
            "is_byok": false,
            "prompt_tokens_details": {
              "cached_tokens": 4370,
              "cache_write_tokens": 18,
              "audio_tokens": 0,
              "video_tokens": 0
            },
            "cost_details": {
              "upstream_inference_cost": 0.04332675,
              "upstream_inference_prompt_cost": 0.00259375,
              "upstream_inference_completions_cost": 0.000733
            },
            "completion_tokens_details": {
              "reasoning_tokens": 1312,
              "image_tokens": 0,
              "audio_tokens": 0
            },
            "server_tool_use_details": {
              "web_search_requests": 4
            }
          }
        }
        """;

    // ══════════════════════════════════════════════════════════════════════
    // 1. Answer extracted correctly
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsAnswer()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Answer.Should().Contain("کارشناسی پیوسته");
        result.Answer.Should().NotBeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 2. Model extracted — returned model has openai/ prefix
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsReturnedModel_WithProviderPrefix()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.ReturnedModel.Should().Be("openai/gpt-6-luna");
        result.RequestedModel.Should().Be("gpt-6-luna");
    }

    // ══════════════════════════════════════════════════════════════════════
    // 3. Provider extracted
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsProvider()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Provider.Should().Be("OpenAI");
    }

    // ══════════════════════════════════════════════════════════════════════
    // 4. Citation extracted — url_citation type, URL and title correct
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsCitation_WithCorrectUrlAndTitle()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Citations.Should().HaveCount(1);
        result.Citations[0].Url.Should().Be("https://tg.me/azaadnews/7252?tr=en&utm_source=openai");
        result.Citations[0].Title.Should().Contain("دانشجویان");
        result.Citations[0].StartIndex.Should().Be(208);
        result.Citations[0].EndIndex.Should().Be(271);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 5. Cost extracted exactly — no precision loss
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsCost_ExactPrecision()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Usage.Cost.Should().Be(0.04332675m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 6. Total tokens extracted
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsTotalTokens()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Usage.TotalTokens.Should().Be(31332);
        result.Usage.PromptTokens.Should().Be(29866);
        result.Usage.CompletionTokens.Should().Be(1466);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 7. Reasoning tokens extracted from completion_tokens_details
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsReasoningTokens_FromCompletionDetails()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Usage.ReasoningTokens.Should().Be(1312);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 8. Web search requests extracted from server_tool_use_details
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsWebSearchRequests()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.Usage.WebSearchRequests.Should().Be(4);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 9. reasoning field is NEVER surfaced — not in answer, not in any property
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_DoesNotExposeReasoning_InAnswer()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");

        // The raw reasoning text must NOT appear in the answer
        result.Answer.Should().NotContain("Searching university admissions");
        result.Answer.Should().NotContain("I need to provide the answer in Persian");

        // The GapGptResponse type must have no reasoning-related property
        var props = typeof(PersianAiChat.Application.Abstractions.GapGptResponse).GetProperties();
        props.Select(p => p.Name.ToLowerInvariant())
             .Should().NotContainMatch("*reasoning*");
    }

    // ══════════════════════════════════════════════════════════════════════
    // 10. reasoning.encrypted data is NEVER exposed in any output field
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_DoesNotExposeEncryptedReasoningData()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");

        // Encrypted data must not appear in answer
        result.Answer.Should().NotContain("ENCRYPTED");
        result.Answer.Should().NotContain("gAAAAAB");   // base64 Fernet prefix

        // Citations must not contain encrypted data
        result.Citations.Should().NotContain(c =>
            c.Url.Contains("ENCRYPTED") || c.Title.Contains("ENCRYPTED"));

        // RequestId is the response ID — not a reasoning ID
        result.RequestId.Should().Be("gen-1790594330-iPBrjQ679xUIWCRa3xaT");
        result.RequestId.Should().NotContain("rs_");    // reasoning detail IDs must not leak
    }

    // ══════════════════════════════════════════════════════════════════════
    // 11. reasoning_details array with mixed types parsed without crash
    //     (summary + encrypted alternating — exact production shape)
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_MixedReasoningDetailsArray_DoesNotThrow()
    {
        var act = () => GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        act.Should().NotThrow("reasoning_details with mixed summary/encrypted entries must deserialize silently");
    }

    // ══════════════════════════════════════════════════════════════════════
    // 12. logprobs: null in choice is tolerated (falls into ExtensionData)
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_NullLogprobs_DoesNotThrow()
    {
        var json = """
            {
              "id": "x", "model": "gpt-6-luna", "provider": "OpenAI",
              "choices": [{
                "index": 0,
                "logprobs": null,
                "finish_reason": "stop",
                "native_finish_reason": "completed",
                "message": { "role": "assistant", "content": "سلام" }
              }],
              "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15, "cost": 0.001 }
            }
            """;
        var raw = JsonSerializer.Deserialize<GapGptRawResponse>(json, JsonOpts)!;
        var act = () => GapGptResponseParser.Parse(raw, "gpt-6-luna");
        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 13. finish_reason and native_finish_reason are correctly mapped
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_ExtractsFinishReasons()
    {
        var result = GapGptResponseParser.Parse(BuildActualResponse(), "gpt-6-luna");
        result.FinishReason.Should().Be("stop");
        result.NativeFinishReason.Should().Be("completed");
    }

    // ══════════════════════════════════════════════════════════════════════
    // 14. Empty choices array → empty answer, no crash
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_EmptyChoices_ReturnsEmptyAnswer()
    {
        var raw = JsonSerializer.Deserialize<GapGptRawResponse>(
            """{"id":"x","model":"gpt","choices":[],"usage":{"prompt_tokens":0,"completion_tokens":0,"total_tokens":0,"cost":0}}""",
            JsonOpts)!;
        var result = GapGptResponseParser.Parse(raw, "gpt");
        result.Answer.Should().BeEmpty();
        result.Citations.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 15. null content → empty answer, no crash
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_NullContent_ReturnsEmptyAnswer()
    {
        var raw = JsonSerializer.Deserialize<GapGptRawResponse>(
            """{"id":"x","model":"gpt","choices":[{"index":0,"message":{"role":"assistant","content":null}}],"usage":{"total_tokens":0,"cost":0}}""",
            JsonOpts)!;
        var result = GapGptResponseParser.Parse(raw, "gpt");
        result.Answer.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 16. Unknown fields at any nesting level do not throw
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_UnknownFieldsAtAllLevels_DoesNotThrow()
    {
        var json = """
            {
              "id": "x", "model": "gpt", "provider": "Test",
              "brand_new_top_level_field": "ignored",
              "choices": [{
                "index": 0,
                "finish_reason": "stop",
                "some_future_field": 42,
                "message": {
                  "role": "assistant",
                  "content": "Hello",
                  "new_message_field": true,
                  "annotations": [
                    {
                      "type": "url_citation",
                      "url_citation": {
                        "url": "https://example.com",
                        "title": "Example",
                        "start_index": 0,
                        "end_index": 5,
                        "future_citation_field": "ignored"
                      },
                      "extra_annotation_field": 99
                    }
                  ]
                }
              }],
              "usage": {
                "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15, "cost": 0.001,
                "new_usage_field": "ignored",
                "server_tool_use_details": { "web_search_requests": 2, "future_tool_field": true }
              }
            }
            """;
        var raw = JsonSerializer.Deserialize<GapGptRawResponse>(json, JsonOpts);
        var act = () => GapGptResponseParser.Parse(raw!, "gpt");
        act.Should().NotThrow();
        var result = act();
        result.Answer.Should().Be("Hello");
        result.Usage.WebSearchRequests.Should().Be(2);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 17. Fixture file parses correctly (integration with saved fixture)
    // ══════════════════════════════════════════════════════════════════════
    [Fact]
    public void Parse_FixtureFile_AllFieldsCorrect()
    {
        var raw = LoadFixture();
        var result = GapGptResponseParser.Parse(raw, "gpt-6-luna");

        result.RequestId.Should().Be("gen-1790594330-iPBrjQ679xUIWCRa3xaT");
        result.ReturnedModel.Should().Be("openai/gpt-6-luna");
        result.Provider.Should().Be("OpenAI");
        result.Answer.Should().NotBeEmpty();
        result.Answer.Should().NotContain("Searching university");   // no reasoning leak
        result.Answer.Should().NotContain("ENCRYPTED");
        result.Citations.Should().HaveCount(1);
        result.Citations[0].Url.Should().StartWith("https://tg.me/azaadnews");
        result.Usage.TotalTokens.Should().Be(31332);
        result.Usage.PromptTokens.Should().Be(29866);
        result.Usage.CompletionTokens.Should().Be(1466);
        result.Usage.ReasoningTokens.Should().Be(1312);
        result.Usage.Cost.Should().Be(0.04332675m);
        result.Usage.WebSearchRequests.Should().Be(4);
        result.FinishReason.Should().Be("stop");
        result.NativeFinishReason.Should().Be("completed");
    }
}

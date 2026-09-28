using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Options;

namespace PersianAiChat.Infrastructure.GapGpt;

public sealed class GapGptService : IGapGptService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GapGptOptions _opts;
    private readonly ILogger<GapGptService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GapGptService(
        IHttpClientFactory httpClientFactory,
        IOptions<GapGptOptions> opts,
        ILogger<GapGptService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<GapGptResponse> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("GapGptClient");

        var request = new GapGptRawRequest
        {
            Model = _opts.Model,
            Messages = messages
                .Select(m => new GapGptRawMessage { Role = m.Role, Content = m.Content })
                .ToList(),
            Tools = [new GapGptTool { Type = "web_search" }]
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            // Post to the configured endpoint URL directly (BaseAddress is the completions endpoint)
            response = await client.PostAsync((string?)null, content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "GapGPT request timed out");
            throw new InvalidOperationException("سرویس هوش مصنوعی در حال حاضر در دسترس نیست.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GapGPT HTTP request failed");
            throw new InvalidOperationException("سرویس هوش مصنوعی در حال حاضر در دسترس نیست.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GapGPT API returned {Status}", response.StatusCode);
            throw new InvalidOperationException("سرویس هوش مصنوعی در حال حاضر در دسترس نیست.");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        GapGptRawResponse? raw;
        try
        {
            raw = JsonSerializer.Deserialize<GapGptRawResponse>(responseJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GapGPT response");
            throw new InvalidOperationException("سرویس هوش مصنوعی پاسخ نامعتبر ارسال کرد.");
        }

        if (raw is null)
            throw new InvalidOperationException("سرویس هوش مصنوعی پاسخ خالی ارسال کرد.");

        _logger.LogInformation(
            "GapGPT success. Id={Id} Model={Model} TotalTokens={Total} WebSearch={WS}",
            raw.Id, raw.Model,
            raw.Usage?.TotalTokens ?? 0,
            raw.Usage?.ServerToolUseDetails?.WebSearchRequests ?? 0);

        return GapGptResponseParser.Parse(raw, _opts.Model);
    }

    public async Task<GapGptResponse> AnalyzeAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("GapGptClient");

        // NO Tools — no web_search index loaded → ~98% fewer prompt tokens
        var request = new GapGptRawRequest
        {
            Model = _opts.Model,
            Messages = messages
                .Select(m => new GapGptRawMessage { Role = m.Role, Content = m.Content })
                .ToList(),
            Tools = null
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync((string?)null, content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "GapGPT analyze request timed out");
            throw new InvalidOperationException("سرویس هوش مصنوعی در حال حاضر در دسترس نیست.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GapGPT analyze HTTP request failed");
            throw new InvalidOperationException("سرویس هوش مصنوعی در حال حاضر در دسترس نیست.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GapGPT analyze returned {Status}", response.StatusCode);
            throw new InvalidOperationException("سرویس هوش مصنوعی در حال حاضر در دسترس نیست.");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        GapGptRawResponse? raw;
        try
        {
            raw = JsonSerializer.Deserialize<GapGptRawResponse>(responseJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GapGPT analyze response");
            throw new InvalidOperationException("سرویس هوش مصنوعی پاسخ نامعتبر ارسال کرد.");
        }

        if (raw is null)
            throw new InvalidOperationException("سرویس هوش مصنوعی پاسخ خالی ارسال کرد.");

        _logger.LogInformation(
            "GapGPT analyze success. Id={Id} TotalTokens={Total} (no web search)",
            raw.Id, raw.Usage?.TotalTokens ?? 0);

        return GapGptResponseParser.Parse(raw, _opts.Model);
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Options;

namespace PersianAiChat.Infrastructure.Sms;

// ────────── SMS.ir DTOs (isolated from rest of app) ──────────

file sealed class SmsIrVerifyRequest
{
    [JsonPropertyName("mobile")]
    public string Mobile { get; init; } = string.Empty;

    [JsonPropertyName("templateId")]
    public int TemplateId { get; init; }

    [JsonPropertyName("parameters")]
    public List<SmsIrParameter> Parameters { get; init; } = [];
}

file sealed class SmsIrParameter
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}

file sealed class SmsIrResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

// ────────── Service ──────────

public sealed class SmsIrService : ISmsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmsIrOptions _opts;
    private readonly ILogger<SmsIrService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmsIrService(
        IHttpClientFactory httpClientFactory,
        IOptions<SmsIrOptions> opts,
        ILogger<SmsIrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(string phone, string otpCode, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(_opts.TemplateId, out var templateId))
        {
            _logger.LogError("SMS.ir TemplateId is not a valid integer: {TemplateId}", _opts.TemplateId);
            return false;
        }

        var request = new SmsIrVerifyRequest
        {
            Mobile = phone,
            TemplateId = templateId,
            Parameters =
            [
                new SmsIrParameter { Name = "AUTHKEY", Value = otpCode }
            ]
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient("SmsIrClient");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/v1/send/verify", content, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "SMS.ir request failed (network error)");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("SMS.ir API returned {Status}", response.StatusCode);
            return false;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            var result = JsonSerializer.Deserialize<SmsIrResponse>(responseJson, JsonOptions);
            if (result?.Status == 1)
            {
                _logger.LogInformation("SMS.ir OTP sent successfully");
                return true;
            }
            _logger.LogWarning("SMS.ir returned non-success status code: {Status}", result?.Status);
            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize SMS.ir response");
            return false;
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Options;

namespace PersianAiChat.Infrastructure.HubSpot;

// ────────── Internal DTOs ──────────

file sealed class HubSpotCreateObjectRequest
{
    [JsonPropertyName("properties")]
    public Dictionary<string, string?> Properties { get; init; } = [];
}

file sealed class HubSpotCreateObjectResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

file sealed class HubSpotAssociationRequest
{
    [JsonPropertyName("inputs")]
    public List<HubSpotAssociationInput> Inputs { get; init; } = [];
}

file sealed class HubSpotAssociationInput
{
    [JsonPropertyName("from")]
    public HubSpotObjectRef From { get; init; } = new();

    [JsonPropertyName("to")]
    public HubSpotObjectRef To { get; init; } = new();

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

file sealed class HubSpotObjectRef
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

file sealed class HubSpotSearchRequest
{
    [JsonPropertyName("filterGroups")]
    public List<FilterGroup> FilterGroups { get; init; } = [];

    [JsonPropertyName("properties")]
    public List<string> Properties { get; init; } = [];

    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 1;
}

file sealed class FilterGroup
{
    [JsonPropertyName("filters")]
    public List<Filter> Filters { get; init; } = [];
}

file sealed class Filter
{
    [JsonPropertyName("propertyName")]
    public string PropertyName { get; init; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}

file sealed class HubSpotSearchResponse
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("results")]
    public List<HubSpotContactResult>? Results { get; init; }
}

file sealed class HubSpotContactResult
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, string?>? Properties { get; init; }
}

// ────────── Service ──────────

public sealed class HubSpotService : IHubSpotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HubSpotOptions _opts;
    private readonly ILogger<HubSpotService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HubSpotService(
        IHttpClientFactory httpClientFactory,
        IOptions<HubSpotOptions> opts,
        ILogger<HubSpotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<HubSpotContact?> FindContactByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("HubSpotClient");

        var body = new HubSpotSearchRequest
        {
            FilterGroups =
            [
                new FilterGroup
                {
                    Filters =
                    [
                        new Filter
                        {
                            PropertyName = "phone",
                            Operator = "EQ",
                            Value = normalizedPhone
                        }
                    ]
                }
            ],
            Properties = ["phone", "firstname", "lastname", "email"],
            Limit = 1
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/crm/v3/objects/contacts/search", content, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "HubSpot API request failed (network error)");
            return null;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("HubSpot API rate limit exceeded (429)");
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogError("HubSpot API authentication error: {Status}", response.StatusCode);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("HubSpot API returned non-success status {Status}", response.StatusCode);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        HubSpotSearchResponse? searchResult;
        try
        {
            searchResult = JsonSerializer.Deserialize<HubSpotSearchResponse>(responseJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize HubSpot search response");
            return null;
        }

        if (searchResult?.Results is null || searchResult.Results.Count == 0)
            return null;

        var contact = searchResult.Results[0];
        if (contact.Id is null) return null;

        var props = contact.Properties ?? new Dictionary<string, string?>();
        return new HubSpotContact(
            contact.Id,
            props.GetValueOrDefault("phone") ?? normalizedPhone,
            props.GetValueOrDefault("firstname"),
            props.GetValueOrDefault("lastname"),
            props.GetValueOrDefault("email")
        );
    }

    public async Task<HubSpotTicketResult> CreateTicketAsync(
        string contactId,
        string subject,
        string analysisBody,
        string category,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("HubSpotClient");
        string? noteId = null;
        string? dealId = null;

        // ── Step 1: Create Note ───────────────────────────────────
        var noteBody = $"موضوع: {subject}\nدسته‌بندی: {category}\n\n{analysisBody}";
        var noteReq = new HubSpotCreateObjectRequest
        {
            Properties = new Dictionary<string, string?>
            {
                ["hs_note_body"] = noteBody,
                ["hs_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            }
        };

        try
        {
            var noteJson = JsonSerializer.Serialize(noteReq, JsonOptions);
            using var noteContent = new StringContent(noteJson, Encoding.UTF8, "application/json");
            var noteResp = await client.PostAsync("/crm/v3/objects/notes", noteContent, cancellationToken);

            if (noteResp.IsSuccessStatusCode)
            {
                var noteResult = JsonSerializer.Deserialize<HubSpotCreateObjectResponse>(
                    await noteResp.Content.ReadAsStringAsync(cancellationToken), JsonOptions);
                noteId = noteResult?.Id;

                if (noteId is not null)
                    await AssociateAsync(client, "notes", noteId, "contacts", contactId,
                        "note_to_contact", cancellationToken);
            }
            else
            {
                _logger.LogWarning("HubSpot note creation failed: {Status}", noteResp.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "HubSpot note creation error (non-fatal)");
        }

        // ── Step 2: Create Deal ───────────────────────────────────
        var dealName = subject.Length > 100 ? subject[..100] : subject;
        var dealReq = new HubSpotCreateObjectRequest
        {
            Properties = new Dictionary<string, string?>
            {
                ["dealname"]    = dealName,
                ["dealstage"]   = "appointmentscheduled",
                ["pipeline"]    = "default",
                ["description"] = $"[{category}] تیکت از طریق دستیار هوشمند"
            }
        };

        try
        {
            var dealJson = JsonSerializer.Serialize(dealReq, JsonOptions);
            using var dealContent = new StringContent(dealJson, Encoding.UTF8, "application/json");
            var dealResp = await client.PostAsync("/crm/v3/objects/deals", dealContent, cancellationToken);

            if (dealResp.IsSuccessStatusCode)
            {
                var dealResult = JsonSerializer.Deserialize<HubSpotCreateObjectResponse>(
                    await dealResp.Content.ReadAsStringAsync(cancellationToken), JsonOptions);
                dealId = dealResult?.Id;

                if (dealId is not null)
                {
                    await AssociateAsync(client, "deals", dealId, "contacts", contactId,
                        "deal_to_contact", cancellationToken);

                    if (noteId is not null)
                        await AssociateAsync(client, "deals", dealId, "notes", noteId,
                            "deal_to_note", cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning("HubSpot deal creation failed: {Status}", dealResp.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "HubSpot deal creation error (non-fatal)");
        }

        var success = dealId is not null || noteId is not null;
        _logger.LogInformation(
            "HubSpot ticket created. DealId={DealId} NoteId={NoteId} ContactId={ContactId}",
            dealId, noteId, contactId);

        return new HubSpotTicketResult(
            success,
            dealId,
            noteId,
            success ? "درخواست شما با موفقیت ثبت شد." : "خطا در ثبت درخواست."
        );
    }

    // ── Private helper ────────────────────────────────────────────
    private async Task AssociateAsync(
        HttpClient client,
        string fromType, string fromId,
        string toType,   string toId,
        string assocType,
        CancellationToken ct)
    {
        try
        {
            var body = new HubSpotAssociationRequest
            {
                Inputs =
                [
                    new HubSpotAssociationInput
                    {
                        From = new HubSpotObjectRef { Id = fromId },
                        To   = new HubSpotObjectRef { Id = toId },
                        Type = assocType
                    }
                ]
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"/crm/v3/associations/{fromType}/{toType}/batch/create";
            var resp = await client.PostAsync(url, content, ct);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning(
                    "HubSpot association {Type} failed: {Status}", assocType, resp.StatusCode);
        }
        catch (Exception ex)
        {
            // Association failure is non-fatal — note and deal still exist
            _logger.LogWarning(ex, "HubSpot association {Type} error (non-fatal)", assocType);
        }
    }
}

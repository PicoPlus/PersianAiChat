namespace PersianAiChat.Application.Abstractions;

/// <summary>HubSpot CRM contact data returned from a lookup.</summary>
public sealed record HubSpotContact(
    string Id,
    string Phone,
    string? FirstName,
    string? LastName,
    string? Email
);

/// <summary>Result of a HubSpot ticket creation (Deal + Note).</summary>
public sealed record HubSpotTicketResult(
    bool Success,
    string? TicketId,   // HubSpot Deal ID
    string? NoteId,     // HubSpot Note ID
    string Message
);

/// <summary>Interacts with the HubSpot CRM API.</summary>
public interface IHubSpotService
{
    /// <summary>
    /// Finds a HubSpot contact by normalized phone number (989xxxxxxxxx).
    /// Returns null if not found or on lookup failure.
    /// </summary>
    Task<HubSpotContact?> FindContactByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a HubSpot Note with the full analysis body and a Deal in the default pipeline.
    /// Associates both to the contact. Direct REST — zero GapGPT tokens.
    /// </summary>
    Task<HubSpotTicketResult> CreateTicketAsync(
        string contactId,
        string subject,
        string analysisBody,
        string category,
        CancellationToken cancellationToken = default);
}

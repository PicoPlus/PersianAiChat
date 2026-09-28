namespace PersianAiChat.Application.Abstractions;

/// <summary>Manages OTP generation, sending, and verification.</summary>
public interface IOtpService
{
    /// <summary>
    /// Requests an OTP for the given phone number. If the phone is not in HubSpot,
    /// returns a generic failure to prevent enumeration.
    /// </summary>
    Task<OtpRequestResult> RequestOtpAsync(string rawPhone, string? requestIp, CancellationToken cancellationToken = default);

    /// <summary>Verifies an OTP and returns authentication result.</summary>
    Task<OtpVerifyResult> VerifyOtpAsync(string rawPhone, string otp, CancellationToken cancellationToken = default);
}

public sealed record OtpRequestResult(bool Success, string Message);

public sealed record OtpVerifyResult(bool Success, string Message, string? UserId = null, string? Phone = null, string? HubSpotContactId = null);

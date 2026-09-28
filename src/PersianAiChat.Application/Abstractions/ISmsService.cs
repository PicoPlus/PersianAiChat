namespace PersianAiChat.Application.Abstractions;

/// <summary>Sends OTP SMS messages via the configured provider.</summary>
public interface ISmsService
{
    /// <summary>Sends an OTP code to the given phone number.</summary>
    Task<bool> SendOtpAsync(string phone, string otpCode, CancellationToken cancellationToken = default);
}

namespace PersianAiChat.Application.Options;

public sealed class GapGptOptions
{
    public const string SectionName = "GapGpt";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://api.gapgpt.app/v1/chat/completions";
    public string Model { get; set; } = "gpt-6-luna";
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class HubSpotOptions
{
    public const string SectionName = "HubSpot";

    public string AccessToken { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.hubapi.com";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class SmsIrOptions
{
    public const string SectionName = "SmsIr";

    public string ApiKey { get; set; } = string.Empty;
    public string LineNumber { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://api.sms.ir/v1";
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int ExpirationMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int MaxRequestsPerWindow { get; set; } = 5;
    public int RateWindowMinutes { get; set; } = 15;
    public int CodeLength { get; set; } = 6;
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string CookieName { get; set; } = ".PersianAiChat.Auth";
    public int SessionLifetimeDays { get; set; } = 7;
    public string JwtSecretKey { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "PersianAiChat";
    public string JwtAudience { get; set; } = "PersianAiChatUsers";
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int OtpRequestsPerWindow { get; set; } = 5;
    public int OtpWindowMinutes { get; set; } = 15;
    public int ChatRequestsPerMinute { get; set; } = 30;
}

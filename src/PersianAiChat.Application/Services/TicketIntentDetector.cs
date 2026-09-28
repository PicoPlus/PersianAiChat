namespace PersianAiChat.Application.Services;

/// <summary>
/// Zero-token keyword-based detector. Decides if a conversation warrants
/// offering the user a ticket submission, based on the user message and
/// the bot's answer.
/// </summary>
internal static class TicketIntentDetector
{
    // Keywords that suggest the user might want to DO something, not just know something
    private static readonly string[] ServiceKeywords =
    [
        "ثبت‌نام", "ثبت نام", "پذیرش", "درخواست", "مدرک", "فرم",
        "وام", "یارانه", "بیمه", "مجوز", "پروانه", "کارت",
        "اقدام", "مراحل", "فرآیند", "چطور باید", "چگونه می‌توان",
        "انتقال", "تغییر رشته", "حذف ترم", "مرخصی تحصیلی",
        "دانشگاه آزاد", "اداره", "سازمان", "شهرداری", "register",
        "apply", "application", "enrollment", "admission"
    ];

    private static readonly string[] UniversityKeywords =
        ["دانشگاه", "دانشجو", "رشته", "کنکور", "ترم", "واحد", "استاد"];

    private static readonly string[] WelfareKeywords =
        ["وام", "بیمه", "یارانه", "مستمری", "حقوق", "بازنشستگی"];

    /// <summary>
    /// Returns true when the conversation suggests an actionable service request.
    /// Pure string matching — costs zero tokens.
    /// </summary>
    internal static bool ShouldOfferTicket(string userMessage, string assistantAnswer)
    {
        var combined = (userMessage + " " + assistantAnswer).ToLowerInvariant();
        return ServiceKeywords.Any(kw => combined.Contains(kw));
    }

    /// <summary>Classifies the request into a HubSpot-friendly category label.</summary>
    internal static string DetectCategory(string message)
    {
        var lower = message.ToLowerInvariant();
        if (UniversityKeywords.Any(kw => lower.Contains(kw))) return "university";
        if (WelfareKeywords.Any(kw => lower.Contains(kw)))    return "welfare";
        return "general";
    }
}

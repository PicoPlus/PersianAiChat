using System.Text.RegularExpressions;

namespace PersianAiChat.Application.Validators;

/// <summary>
/// Normalizes Iranian mobile phone numbers to canonical HubSpot format: 989xxxxxxxxx
/// </summary>
public static class PhoneNormalizer
{
    // Persian digit map: ۰-۹ (U+06F0–U+06F9) and Arabic ٠-٩ (U+0660–U+0669)
    private static readonly char[] PersianDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];
    private static readonly char[] ArabicDigits  = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

    /// <summary>
    /// Attempts to normalize a raw phone input to the canonical 989xxxxxxxxx format.
    /// Returns (true, normalizedPhone, null) on success.
    /// Returns (false, null, persianErrorMessage) on failure.
    /// </summary>
    public static (bool Success, string? Normalized, string? ErrorMessage) TryNormalize(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return (false, null, "شماره موبایل را وارد کنید.");

        // 1. Strip all whitespace including Persian/Arabic ZWNJ and zero-width chars
        var cleaned = rawInput.Trim();
        cleaned = Regex.Replace(cleaned, @"[\s\u200B\u200C\u200D\uFEFF]+", string.Empty);

        // 2. Normalize Persian digits to ASCII
        for (int i = 0; i < 10; i++)
        {
            cleaned = cleaned.Replace(PersianDigits[i], (char)('0' + i));
            cleaned = cleaned.Replace(ArabicDigits[i], (char)('0' + i));
        }

        // 3. Reject anything still containing non-digit characters except leading +
        if (!Regex.IsMatch(cleaned, @"^\+?\d+$"))
            return (false, null, "شماره موبایل وارد‌شده معتبر نیست.");

        // 4. Normalize leading formats
        string digits;
        if (cleaned.StartsWith('+'))
            digits = cleaned[1..]; // strip +
        else
            digits = cleaned;

        // reject double-zero prefix (00989...)
        if (digits.StartsWith("00"))
            return (false, null, "شماره موبایل وارد‌شده معتبر نیست.");

        string normalized;

        if (digits.StartsWith("09") && digits.Length == 11)
        {
            // 09xxxxxxxxx → 98xxxxxxxxx
            normalized = "98" + digits[1..];
        }
        else if (digits.StartsWith("989") && digits.Length == 12)
        {
            normalized = digits;
        }
        else if (digits.StartsWith("9") && digits.Length == 11 && !digits.StartsWith("98"))
        {
            // 9xxxxxxxxx without country code prefix — not standard, reject
            return (false, null, "شماره موبایل وارد‌شده معتبر نیست.");
        }
        else
        {
            return (false, null, "شماره موبایل وارد‌شده معتبر نیست.");
        }

        // 5. Validate Iranian mobile prefixes (9[0-9]...)
        // After normalization it must be exactly 989xxxxxxxxx (12 digits)
        if (!Regex.IsMatch(normalized, @"^989[0-9]{9}$"))
            return (false, null, "شماره موبایل وارد‌شده معتبر نیست.");

        return (true, normalized, null);
    }
}

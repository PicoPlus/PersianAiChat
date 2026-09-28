using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Options;
using PersianAiChat.Application.Validators;
using PersianAiChat.Domain.Entities;

namespace PersianAiChat.Application.Services;

/// <summary>Handles OTP lifecycle: request, rate-limit, verify, invalidate.</summary>
public sealed class OtpService : IOtpService
{
    private readonly IApplicationDbContext _db;
    private readonly IHubSpotService _hubSpot;
    private readonly ISmsService _sms;
    private readonly OtpOptions _opts;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IApplicationDbContext db,
        IHubSpotService hubSpot,
        ISmsService sms,
        IOptions<OtpOptions> opts,
        ILogger<OtpService> logger)
    {
        _db = db;
        _hubSpot = hubSpot;
        _sms = sms;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<OtpRequestResult> RequestOtpAsync(string rawPhone, string? requestIp, CancellationToken cancellationToken = default)
    {
        var (ok, normalized, error) = PhoneNormalizer.TryNormalize(rawPhone);
        if (!ok || normalized is null)
            return new OtpRequestResult(false, error ?? "اطلاعات واردشده قابل تأیید نیست.");

        // Check rate limit
        var windowStart = DateTime.UtcNow.AddMinutes(-_opts.RateWindowMinutes);
        var recentCount = await _db.OtpRequests
            .CountAsync(r => r.Phone == normalized && r.CreatedAt >= windowStart, cancellationToken);

        if (recentCount >= _opts.MaxRequestsPerWindow)
        {
            _logger.LogWarning("OTP rate limit exceeded for phone hash. Count={Count}", recentCount);
            return new OtpRequestResult(false, "تعداد درخواست‌های ارسال کد بیش از حد مجاز است. لطفاً بعداً تلاش کنید.");
        }

        // Verify contact exists in HubSpot — generic message on failure
        var contact = await _hubSpot.FindContactByPhoneAsync(normalized, cancellationToken);
        if (contact is null)
        {
            _logger.LogInformation("OTP requested for phone not in HubSpot.");
            // Generic message — do NOT reveal whether phone exists
            return new OtpRequestResult(true, "در صورت تأیید اطلاعات، کد تأیید ارسال خواهد شد.");
        }

        // Generate secure OTP
        var code = GenerateSecureOtp();
        var hash = HashCode(code);
        var ipHash = requestIp is not null ? HashCode(requestIp) : null;

        // Invalidate previous unused OTPs for this phone
        var previous = await _db.OtpRequests
            .Where(r => r.Phone == normalized && r.UsedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        foreach (var prev in previous)
            prev.UsedAt = DateTime.UtcNow; // mark as consumed

        var otpRequest = new OtpRequest
        {
            Phone = normalized,
            CodeHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_opts.ExpirationMinutes),
            MaxAttempts = _opts.MaxAttempts,
            RequestIpHash = ipHash
        };

        _db.OtpRequests.Add(otpRequest);
        await _db.SaveChangesAsync(cancellationToken);

        // Send SMS — do not expose internal errors to the caller
        var sent = await _sms.SendOtpAsync(normalized, code, cancellationToken);
        if (!sent)
        {
            _logger.LogError("SMS delivery failed for OTP request {OtpId}", otpRequest.Id);
            // Still return generic success to prevent timing-based enumeration
        }

        return new OtpRequestResult(true, "در صورت تأیید اطلاعات، کد تأیید ارسال خواهد شد.");
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(string rawPhone, string otp, CancellationToken cancellationToken = default)
    {
        var (ok, normalized, error) = PhoneNormalizer.TryNormalize(rawPhone);
        if (!ok || normalized is null)
            return new OtpVerifyResult(false, error ?? "اطلاعات واردشده قابل تأیید نیست.");

        if (string.IsNullOrWhiteSpace(otp) || otp.Length != _opts.CodeLength || !otp.All(char.IsDigit))
            return new OtpVerifyResult(false, "کد تأیید معتبر نیست.");

        var otpRequest = await _db.OtpRequests
            .Where(r => r.Phone == normalized && r.UsedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpRequest is null)
            return new OtpVerifyResult(false, "کد تأیید منقضی شده یا معتبر نیست.");

        // Increment attempt counter
        otpRequest.Attempts++;
        await _db.SaveChangesAsync(cancellationToken);

        if (otpRequest.Attempts > otpRequest.MaxAttempts)
        {
            otpRequest.UsedAt = DateTime.UtcNow; // invalidate after max attempts
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerifyResult(false, "تعداد تلاش‌های مجاز تمام شده. لطفاً کد جدیدی درخواست کنید.");
        }

        var inputHash = HashCode(otp);
        if (!string.Equals(inputHash, otpRequest.CodeHash, StringComparison.Ordinal))
            return new OtpVerifyResult(false, "کد تأیید اشتباه است.");

        // Mark OTP as used immediately to prevent replay
        otpRequest.UsedAt = DateTime.UtcNow;

        // Upsert user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == normalized, cancellationToken);
        if (user is null)
        {
            var contact = await _hubSpot.FindContactByPhoneAsync(normalized, cancellationToken);
            user = new User
            {
                Phone = normalized,
                HubSpotContactId = contact?.Id ?? string.Empty,
                LastLoginAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User authenticated. UserId={UserId}", user.Id);
        return new OtpVerifyResult(true, "احراز هویت موفق.", user.Id.ToString(), normalized, user.HubSpotContactId);
    }

    private string GenerateSecureOtp()
    {
        // Use cryptographically secure random
        Span<byte> bytes = stackalloc byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var value = (int)(BitConverter.ToUInt32(bytes) % 1_000_000);
        return value.ToString("D6");
    }

    private static string HashCode(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}

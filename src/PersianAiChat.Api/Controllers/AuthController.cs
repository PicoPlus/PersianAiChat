using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Infrastructure.Authentication;

namespace PersianAiChat.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IOtpService _otpService;
    private readonly JwtTokenService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOtpService otpService,
        JwtTokenService jwtService,
        ILogger<AuthController> logger)
    {
        _otpService = otpService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>Request an OTP for the given phone number.</summary>
    [HttpPost("request-otp")]
    [EnableRateLimiting("OtpPolicy")]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpRequest body,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _otpService.RequestOtpAsync(body.Phone, ip, cancellationToken);
        return Ok(new { success = result.Success, message = result.Message });
    }

    /// <summary>Verify an OTP and establish an authenticated session.</summary>
    [HttpPost("verify-otp")]
    [EnableRateLimiting("OtpPolicy")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest body,
        CancellationToken cancellationToken)
    {
        var result = await _otpService.VerifyOtpAsync(body.Phone, body.Otp, cancellationToken);
        if (!result.Success)
            return Ok(new { success = false, authenticated = false, message = result.Message });

        // Issue authentication cookie
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId!),
            new("phone", result.Phone!),
            new("hubspotContactId", result.HubSpotContactId ?? string.Empty)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });

        _logger.LogInformation("User authenticated via cookie. UserId={UserId}", result.UserId);
        return Ok(new { success = true, authenticated = true });
    }

    /// <summary>Returns the current authenticated user info.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var phone = User.FindFirstValue("phone");
        var hubspotId = User.FindFirstValue("hubspotContactId");

        return Ok(new
        {
            authenticated = true,
            user = new { id = userId, phone, hubspotContactId = hubspotId }
        });
    }

    /// <summary>Signs out the current user.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }
}

public sealed record RequestOtpRequest(string Phone);
public sealed record VerifyOtpRequest(string Phone, string Otp);

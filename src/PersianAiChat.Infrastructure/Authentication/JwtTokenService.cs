using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PersianAiChat.Application.Options;

namespace PersianAiChat.Infrastructure.Authentication;

/// <summary>Issues and validates JWT tokens for API clients.</summary>
public sealed class JwtTokenService
{
    private readonly SecurityOptions _opts;

    public JwtTokenService(IOptions<SecurityOptions> opts) => _opts = opts.Value;

    public string CreateToken(string userId, string phone, string hubspotContactId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.JwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("phone", phone),
            new Claim("hubspotContactId", hubspotContactId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _opts.JwtIssuer,
            audience: _opts.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_opts.SessionLifetimeDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

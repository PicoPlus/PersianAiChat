using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PersianAiChat.Application;
using PersianAiChat.Application.Options;
using PersianAiChat.Infrastructure;
using PersianAiChat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ───────────────────────────────────────────────────

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PersianAiChat API",
        Version = "v1",
        Description = "Persian AI Chatbot Backend API"
    });
    c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = ".PersianAiChat.Auth",
        Description = "Authentication cookie"
    });
});

// Authentication — HttpOnly cookie-based
var securityOpts = builder.Configuration
    .GetSection(SecurityOptions.SectionName)
    .Get<SecurityOptions>() ?? new SecurityOptions();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = securityOpts.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(securityOpts.SessionLifetimeDays);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                "{\"status\":401,\"title\":\"دسترسی شما نیاز به احراز هویت دارد.\"}");
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                "{\"status\":403,\"title\":\"دسترسی شما مجاز نیست.\"}");
        };
    });

builder.Services.AddAuthorization();

// Antiforgery for cookie-based auth
builder.Services.AddAntiforgery(opts =>
{
    opts.HeaderName = "X-XSRF-TOKEN";
    opts.Cookie.Name = "XSRF-TOKEN";
    opts.Cookie.HttpOnly = false; // JS needs to read it
    opts.Cookie.SameSite = SameSiteMode.Strict;
});

// Rate limiting
var rateLimitOpts = builder.Configuration
    .GetSection(RateLimitOptions.SectionName)
    .Get<RateLimitOptions>() ?? new RateLimitOptions();

builder.Services.AddRateLimiter(opts =>
{
    opts.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"status\":429,\"title\":\"تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.\"}");
    };

    // OTP endpoints: 5 req / 15 min per IP
    opts.AddFixedWindowLimiter("OtpPolicy", limiterOpts =>
    {
        limiterOpts.Window = TimeSpan.FromMinutes(rateLimitOpts.OtpWindowMinutes);
        limiterOpts.PermitLimit = rateLimitOpts.OtpRequestsPerWindow;
        limiterOpts.QueueLimit = 0;
        limiterOpts.AutoReplenishment = true;
    });

    // Chat: 30 req / min per authenticated user
    opts.AddFixedWindowLimiter("ChatPolicy", limiterOpts =>
    {
        limiterOpts.Window = TimeSpan.FromMinutes(1);
        limiterOpts.PermitLimit = rateLimitOpts.ChatRequestsPerMinute;
        limiterOpts.QueueLimit = 0;
        limiterOpts.AutoReplenishment = true;
    });
});

// CORS — restrict in production
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            var allowed = builder.Configuration["AllowedOrigins"]?.Split(',') ?? [];
            policy.WithOrigins(allowed).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }
    });
});

// Response compression
builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);

// ─── Build ───────────────────────────────────────────────────────

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PersianAiChat API v1"));
}

app.UseResponseCompression();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=()";
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static assets for 1 year (versioned by filename hash)
        if (ctx.File.Name.Contains('.') && !ctx.File.Name.EndsWith(".html"))
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
    }
});

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Antiforgery middleware — set XSRF cookie on every request
app.Use(async (ctx, next) =>
{
    var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
    var tokens = antiforgery.GetAndStoreTokens(ctx);
    ctx.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
    {
        HttpOnly = false,
        SameSite = SameSiteMode.Strict,
        Secure = ctx.Request.IsHttps
    });
    await next();
});

app.MapControllers();

// SPA fallback — serve index.html for non-API, non-static routes
app.MapFallbackToFile("index.html");

app.Run();

// Required for integration test WebApplicationFactory
public partial class Program { }

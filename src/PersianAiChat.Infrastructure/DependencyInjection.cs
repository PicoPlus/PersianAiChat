using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Options;
using PersianAiChat.Infrastructure.Authentication;
using PersianAiChat.Infrastructure.GapGpt;
using PersianAiChat.Infrastructure.HubSpot;
using PersianAiChat.Infrastructure.Persistence;
using PersianAiChat.Infrastructure.Sms;

namespace PersianAiChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<GapGptOptions>(configuration.GetSection(GapGptOptions.SectionName));
        services.Configure<HubSpotOptions>(configuration.GetSection(HubSpotOptions.SectionName));
        services.Configure<SmsIrOptions>(configuration.GetSection(SmsIrOptions.SectionName));
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        // Database
        services.AddDbContext<ApplicationDbContext>(opts =>
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? "Data Source=persianaichat.db";
            opts.UseSqlite(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Data Protection — persist keys to DB so they survive container restarts
        services.AddDataProtection()
            .SetApplicationName("PersianAiChat")
            .PersistKeysToDbContext<ApplicationDbContext>();

        // HTTP Clients
        services.AddHttpClient("HubSpotClient", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<HubSpotOptions>>().Value;
            client.BaseAddress = new Uri(opts.ApiBaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opts.AccessToken);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddHttpClient("SmsIrClient", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SmsIrOptions>>().Value;
            client.BaseAddress = new Uri(opts.ApiUrl);
            client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddHttpClient("GapGptClient", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<GapGptOptions>>().Value;
            // BaseAddress must end with '/' for relative PostAsync paths to work correctly
            var apiUrl = opts.ApiUrl.TrimEnd('/') + '/';
            client.BaseAddress = new Uri(apiUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opts.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        // Services
        services.AddScoped<IHubSpotService, HubSpotService>();
        services.AddScoped<ISmsService, SmsIrService>();
        services.AddScoped<IGapGptService, GapGptService>();
        services.AddSingleton<JwtTokenService>();

        return services;
    }
}

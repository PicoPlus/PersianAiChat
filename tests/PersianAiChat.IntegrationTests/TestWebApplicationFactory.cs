using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Infrastructure.Persistence;

namespace PersianAiChat.IntegrationTests;

/// <summary>Test web application factory with in-memory database.</summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove real DB context
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add in-memory DB
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase("IntegrationTestDb_" + Guid.NewGuid()));

            services.AddScoped<IApplicationDbContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());

            // Replace HubSpot, SMS, GapGPT with mocks — done per test via WithWebHostBuilder
        });
    }
}

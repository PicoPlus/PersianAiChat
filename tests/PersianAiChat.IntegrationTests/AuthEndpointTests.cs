using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.IntegrationTests;

public class AuthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestOtp_InvalidPhone_ReturnsBadRequest()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mock = new Mock<IHubSpotService>();
                mock.Setup(h => h.FindContactByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((HubSpotContact?)null);
                services.AddScoped<IHubSpotService>(_ => mock.Object);

                var smsMock = new Mock<ISmsService>();
                smsMock.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(true);
                services.AddScoped<ISmsService>(_ => smsMock.Object);
            });
        }).CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/request-otp", new { phone = "0912" });
        // Should return 200 with success:false (validation failure)
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<OtpResponse>();
        body!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RequestOtp_PhoneNotInHubSpot_ReturnsGenericMessage()
    {
        var hubMock = new Mock<IHubSpotService>();
        hubMock.Setup(h => h.FindContactByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((HubSpotContact?)null);
        var smsMock = new Mock<ISmsService>();

        var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddScoped<IHubSpotService>(_ => hubMock.Object);
            s.AddScoped<ISmsService>(_ => smsMock.Object);
        })).CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/request-otp", new { phone = "09122222222" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<OtpResponse>();
        // Generic message — does not say "number not found"
        body!.Message.Should().NotBeNullOrEmpty();
        body.Message.Should().NotContain("وجود ندارد");
    }

    [Fact]
    public async Task Me_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var resp = await client.GetAsync("/api/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record OtpResponse(bool Success, string? Message);
}

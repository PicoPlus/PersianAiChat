using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Options;
using PersianAiChat.Application.Services;
using PersianAiChat.Infrastructure.Persistence;

namespace PersianAiChat.UnitTests;

public class OtpServiceTests
{
    private static ApplicationDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static OtpOptions DefaultOptions() => new()
    {
        ExpirationMinutes = 5,
        MaxAttempts = 5,
        MaxRequestsPerWindow = 5,
        RateWindowMinutes = 15,
        CodeLength = 6
    };

    private static (OtpService svc, Mock<IHubSpotService> hubspot, Mock<ISmsService> sms, ApplicationDbContext db) Build(OtpOptions? opts = null)
    {
        var db = CreateDb();
        var hubspot = new Mock<IHubSpotService>();
        var sms = new Mock<ISmsService>();
        var options = Options.Create(opts ?? DefaultOptions());
        var logger = NullLogger<OtpService>.Instance;
        var svc = new OtpService(db, hubspot.Object, sms.Object, options, logger);
        return (svc, hubspot, sms, db);
    }

    [Fact]
    public async Task RequestOtp_PhoneNotInHubSpot_ReturnsGenericSuccess()
    {
        var (svc, hubspot, sms, _) = Build();
        hubspot.Setup(h => h.FindContactByPhoneAsync("989122222222", It.IsAny<CancellationToken>()))
               .ReturnsAsync((HubSpotContact?)null);

        var result = await svc.RequestOtpAsync("09122222222", null);

        // Must return success:true (does not reveal phone absence)
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
        // SMS must NOT be sent
        sms.Verify(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestOtp_PhoneInHubSpot_SendsOtp()
    {
        var (svc, hubspot, sms, _) = Build();
        hubspot.Setup(h => h.FindContactByPhoneAsync("989122222222", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new HubSpotContact("1", "989122222222", "علی", "رضایی", null));
        sms.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        var result = await svc.RequestOtpAsync("09122222222", null);

        result.Success.Should().BeTrue();
        sms.Verify(s => s.SendOtpAsync("989122222222", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestOtp_InvalidPhone_ReturnsFailure()
    {
        var (svc, _, _, _) = Build();
        var result = await svc.RequestOtpAsync("0912", null);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyOtp_CorrectCode_Succeeds()
    {
        var (svc, hubspot, sms, db) = Build();
        hubspot.Setup(h => h.FindContactByPhoneAsync("989122222222", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new HubSpotContact("42", "989122222222", null, null, null));
        sms.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        // Request OTP — this creates the record in DB
        await svc.RequestOtpAsync("09122222222", null);

        // Get the OTP hash from DB and reverse-engineer the plaintext via brute force
        // Actually: directly inject a known OTP code for testing
        var otpRecord = await db.OtpRequests.OrderByDescending(r => r.CreatedAt).FirstAsync();

        // Hash "123456" and compare — instead, set hash directly
        var testCode = "123456";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(testCode));
        otpRecord.CodeHash = Convert.ToHexString(hash);
        await db.SaveChangesAsync();

        var result = await svc.VerifyOtpAsync("09122222222", testCode);
        result.Success.Should().BeTrue();
        result.Phone.Should().Be("989122222222");
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_Fails()
    {
        var (svc, hubspot, sms, db) = Build();
        hubspot.Setup(h => h.FindContactByPhoneAsync("989122222222", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new HubSpotContact("42", "989122222222", null, null, null));
        sms.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        await svc.RequestOtpAsync("09122222222", null);

        var result = await svc.VerifyOtpAsync("09122222222", "000000");
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyOtp_ExpiredCode_Fails()
    {
        var (svc, hubspot, sms, db) = Build(new OtpOptions
        {
            ExpirationMinutes = -1, // already expired
            MaxAttempts = 5,
            MaxRequestsPerWindow = 100,
            RateWindowMinutes = 15,
            CodeLength = 6
        });
        hubspot.Setup(h => h.FindContactByPhoneAsync("989122222222", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new HubSpotContact("42", "989122222222", null, null, null));
        sms.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        await svc.RequestOtpAsync("09122222222", null);

        var result = await svc.VerifyOtpAsync("09122222222", "123456");
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("منقضی");
    }

    [Fact]
    public async Task RequestOtp_RateLimit_RejectsAfterMaxRequests()
    {
        var (svc, hubspot, sms, _) = Build(new OtpOptions
        {
            ExpirationMinutes = 5,
            MaxAttempts = 5,
            MaxRequestsPerWindow = 2,
            RateWindowMinutes = 15,
            CodeLength = 6
        });
        hubspot.Setup(h => h.FindContactByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new HubSpotContact("1", "989122222222", null, null, null));
        sms.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        await svc.RequestOtpAsync("09122222222", null);
        await svc.RequestOtpAsync("09122222222", null);
        var result = await svc.RequestOtpAsync("09122222222", null);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("درخواست");
    }

    [Fact]
    public async Task VerifyOtp_OtpUsedAfterSuccess_CannotBeUsedAgain()
    {
        var (svc, hubspot, sms, db) = Build();
        hubspot.Setup(h => h.FindContactByPhoneAsync("989122222222", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new HubSpotContact("42", "989122222222", null, null, null));
        sms.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        await svc.RequestOtpAsync("09122222222", null);
        var otpRecord = await db.OtpRequests.OrderByDescending(r => r.CreatedAt).FirstAsync();
        var testCode = "654321";
        otpRecord.CodeHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(testCode)));
        await db.SaveChangesAsync();

        var first = await svc.VerifyOtpAsync("09122222222", testCode);
        first.Success.Should().BeTrue();

        var second = await svc.VerifyOtpAsync("09122222222", testCode);
        second.Success.Should().BeFalse(); // replay prevented
    }
}

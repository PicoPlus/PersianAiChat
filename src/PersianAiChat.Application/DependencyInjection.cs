using Microsoft.Extensions.DependencyInjection;
using PersianAiChat.Application.Abstractions;
using PersianAiChat.Application.Services;

namespace PersianAiChat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IUsageService, UsageService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITicketService, TicketService>();
        return services;
    }
}

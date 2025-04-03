using SignalR.Client.Services.Interfaces;
using SignalR.Client.Services;

namespace SignalR.Client.Shared;

public static class ServiceConfiguration
{
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        //services.AddScoped<IChatService, ChatService>();
        services.AddTransient<IChatService, ChatService>();
        return services;
    }
}
using SignalR.Client.Services.Interfaces;
using SignalR.Client.Services;

namespace SignalR.Client.Shared;

public static class ServiceConfiguration
{
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        // Since the Client is driving this process I am making this Transient.  Normally if the server were
        // to drive this process (Server Side registration) then the Service would favor a "Scoped" approach.
        services.AddTransient<IChatService, ChatService>();
        return services;
    }
}
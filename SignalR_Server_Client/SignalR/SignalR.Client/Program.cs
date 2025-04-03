using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SignalR.Client.Services;
using SignalR.Client.Services.Interfaces;
using SignalR.Client.Shared;

namespace SignalR.Client;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // This is the correct place for registration of the ChatService.
        // This should always go in the client project, not the server project (for interactive BOTH Server/Client solution).
        // But according to Server/Client we need to create a ServiceConfiguration which is available to both Client and Server.  
        // Tried this by itself and it throws errors on page refreshing.

        // Replacing this with the Shared Service Configurations.
        //builder.Services.AddScoped<IChatService, ChatService>();  

        // New Approach due to Server/WebAssembly interaction - Service Configurations.
        builder.Services.AddChatServices();

        await builder.Build().RunAsync();
    }
}
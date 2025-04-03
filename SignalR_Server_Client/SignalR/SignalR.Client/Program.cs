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

        // New Approach due to Server/WebAssembly interaction - Service Configurations.
        // Register SignalR chat services for client-side hub communication.
        // Uses transient lifetime via shared ServiceConfiguration for WebAssembly mode.
        // This should always go in the client project, not the server project (for interactive BOTH Server/Client solution).
        // But according to Server/Client we need to create a ServiceConfiguration which is available to both Client and Server.  
        builder.Services.AddChatServices();

        await builder.Build().RunAsync();
    }
}
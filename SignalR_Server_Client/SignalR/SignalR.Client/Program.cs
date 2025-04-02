using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SignalR.Client.Services;
using SignalR.Client.Services.Interfaces;

namespace SignalR.Client;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddScoped<IChatService, ChatService>();

        await builder.Build().RunAsync();
    }
}
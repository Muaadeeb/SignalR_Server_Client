using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SignalR.Client.Services;

namespace SignalR.Client;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddScoped<ChatService>();

        await builder.Build().RunAsync();
    }
}



//internal class Program
//{
//    static async Task Main(string[] args)
//    {
//        var builder = WebAssemblyHostBuilder.CreateDefault(args);

//        // Register the root component (App.razor)
//        builder.RootComponents.Add<App>("#app");
//        builder.RootComponents.Add<HeadOutlet>("head::after");

//        // Register ChatService for dependency injection
//        builder.Services.AddScoped<ChatService>();

//        await builder.Build().RunAsync();
//    }
//}
using SignalR.Components;
using SignalR.Hubs;
using SignalR.Client.Shared;

namespace SignalR;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add Blazor services with SignalR support for both render modes.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = true; // Enable detailed SignalR circuit errors for debugging.
            })
            .AddInteractiveWebAssemblyComponents();
        builder.Services.AddSignalR();

        // Register shared chat services for SignalR client compatibility.
        // Required here for SSR prerendering despite client-side preference (InteractiveWebAssembly).
        builder.Services.AddChatServices();

        // Configure CORS to allow SignalR hub connections from any origin.
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders("Content-Disposition");
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();

        // Map Blazor components with SignalR render mode support.
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        // Map SignalR hub for real-time chat functionality.
        app.MapHub<ChatHub>("hubs/chathub");

        app.UseCors("AllowAll");
        app.Run();
    }
}
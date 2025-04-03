using Microsoft.AspNetCore.SignalR;
using SignalR.Client.Pages;
using SignalR.Client.Services.Interfaces;
using SignalR.Client.Services;
using SignalR.Components;
using SignalR.Hubs;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Win32;
using System.Drawing;
using System;
using SignalR.Client.Shared;

namespace SignalR;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = true; // Enable detailed circuit errors
            })
            .AddInteractiveWebAssemblyComponents();
        builder.Services.AddSignalR();


        // Add Shared / Common services
        // Register ChatService Only in the Client (Shared Registration):
        // Rely on the client-side DI for both modes. The server’s job is to host ChatHub, not manage client services.
        // this is needed if we run wasm on a different domain or port.  
        builder.Services.AddChatServices();
        
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

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();

        // This means the client assembly(SignalR.Client) is already included, and its DI registrations(from SignalR.Client.Program.cs)
        // are available in WebAssembly mode. For InteractiveServer mode, Blazor creates a circuit-scoped DI container on the server,
        // but it doesn’t automatically inherit WebAssembly-specific registrations.
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        // InteractiveServer Mode: When Chat.razor runs server - side, it still gets IChatService injected because Blazor’s
        // DI system resolves it from the circuit scope.The server doesn’t need its own instance—it’s the client - side
        // logic connecting to ChatHub.
        //
        // InteractiveWebAssembly Mode: The WebAssembly app uses its own DI container(from SignalR.Client.Program.cs), and
        // ChatService connects to the hub from the browser.
        //
        // Single Registration: ChatService is only registered in SignalR.Client, maintaining separation of concerns.
        app.MapHub<ChatHub>("hubs/chathub");

        app.UseCors("AllowAll");
        app.Run();
    }
}



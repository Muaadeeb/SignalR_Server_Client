using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;

namespace SignalR.Client.Services
{
    public class ChatService
    {
        private readonly HubConnection _connection;

        // Events for UI to subscribe to
        public event Action<string, string>? OnMessageReceived;        // General message received
        public event Action<string>? OnUserJoined;                     // User joins the chat
        public event Action<string>? OnUserLeft;                       // User leaves the chat
        public event Action? OnConnected;                              // Connection established
        public event Action? OnDisconnected;                           // Connection lost
        public event Action? OnReconnecting;                           // Attempting to reconnect
        public event Action? OnReconnected;                            // Successfully reconnected
        public event Action<string>? OnError;                          // Error messages

        public ChatService()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7021/hubs/chathub")
                .WithAutomaticReconnect()
                .Build();

            // Register hub message handlers
            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                OnMessageReceived?.Invoke(user, message);
            });

            _connection.On<string>("UserJoined", (user) =>
            {
                OnUserJoined?.Invoke(user);
            });

            _connection.On<string>("UserLeft", (user) =>
            {
                OnUserLeft?.Invoke(user);
            });

            // Handle connection state changes
            _connection.Closed += async (error) =>
            {
                OnDisconnected?.Invoke();
                OnError?.Invoke(error?.Message ?? "Connection closed unexpectedly");
                await Task.CompletedTask; // Required for async event handler
            };

            _connection.Reconnecting += async (error) =>
            {
                OnReconnecting?.Invoke();
                OnError?.Invoke(error?.Message ?? "Reconnecting...");
                await Task.CompletedTask;
            };

            _connection.Reconnected += async (connectionId) =>
            {
                OnReconnected?.Invoke();
                Console.WriteLine($"Reconnected with ID: {connectionId}");
                await Task.CompletedTask;
            };

            // Start the connection
            StartConnectionAsync();
        }

        private async void StartConnectionAsync()
        {
            try
            {
                await _connection.StartAsync();
                OnConnected?.Invoke();
                Console.WriteLine("Connected to SignalR hub!");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                Console.WriteLine($"Connection failed: {ex.Message}");
            }
        }

        // Public methods to invoke hub actions
        public async Task SendMessage(string user, string message)
        {
            try
            {
                await _connection.InvokeAsync("SendMessage", user, message);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to send message: {ex.Message}");
            }
        }

        public async Task JoinChat(string user)
        {
            try
            {
                await _connection.InvokeAsync("JoinChat", user);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to join chat: {ex.Message}");
            }
        }

        public async Task LeaveChat(string user)
        {
            try
            {
                await _connection.InvokeAsync("LeaveChat", user);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to leave chat: {ex.Message}");
            }
        }

        // Connection state properties
        public bool IsConnected => _connection.State == HubConnectionState.Connected;
        public string ConnectionId => _connection.ConnectionId ?? "Not connected";

        // Manual stop (optional)
        public async Task StopAsync()
        {
            await _connection.StopAsync();
        }
    }
}
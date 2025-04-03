using Microsoft.AspNetCore.SignalR.Client;
using SignalR.Client.Services.Interfaces;

namespace SignalR.Client.Services
{
    public class ChatService : IChatService
    {
        private readonly HubConnection _connection;
        private string currentUser = "";
        private List<string> joinedGroups = new();
        public IReadOnlyList<string> JoinedGroups => joinedGroups.AsReadOnly();

        // Existing Events
        public event Action<string, string>? OnMessageReceived;
        public event Action<string, string>? OnGroupMessageReceived;
        public event Action<string>? OnUserJoined;
        public event Action<string>? OnUserLeft;
        public event Action<List<string>>? OnUserListUpdated;
        public event Action<string>? OnGroupMessage;
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action? OnReconnecting;
        public event Action? OnReconnected;
        public event Action<string>? OnError;
        
        // New Event
        public event Action<string, string>? OnPrivateMessageReceived;

        public ChatService()
        {
            Console.WriteLine($"ChatService instantiated, Not yet connected");

            // Initialize SignalR hub connection to "Selected Route" with automatic reconnect.
            _connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7021/hubs/chathub")
                .WithAutomaticReconnect()
                .Build();

            Console.WriteLine($"ChatService instantiated, ConnectionId: {_connection.ConnectionId ?? "Not yet connected"}");
            
            // Register SignalR hub event handlers.
            _connection.On<string, string>("ReceiveMessage", (user, message) =>
                OnMessageReceived?.Invoke(user, message));

            _connection.On<string, string, string>("ReceiveGroupMessage", (user, groupName, message) =>
            {
                Console.WriteLine($"Received group message from {user} in {groupName}: {message}");
                OnGroupMessageReceived?.Invoke(user, $"{groupName}|{message}");
            });

            _connection.On<string>("UserJoined", (user) =>
                OnUserJoined?.Invoke(user));

            _connection.On<string>("UserLeft", (user) =>
                OnUserLeft?.Invoke(user));

            _connection.On<List<string>>("UpdateUserList", (users) =>
                OnUserListUpdated?.Invoke(users));

            _connection.On<string>("GroupMessage", (message) =>
                OnGroupMessage?.Invoke(message));

            _connection.Closed += async (error) =>
            {
                OnDisconnected?.Invoke();
                OnError?.Invoke(error?.Message ?? "Connection closed");
                await Task.CompletedTask;
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
                if (!string.IsNullOrEmpty(currentUser))
                {
                    await _connection.InvokeAsync("JoinChat", currentUser);
                    Console.WriteLine($"Reconnected and rejoined chat as {currentUser}, ConnectionId: {connectionId}");
                    foreach (var group in joinedGroups.ToList())
                    {
                        await _connection.InvokeAsync("JoinGroup", group);
                        Console.WriteLine($"Rejoined group {group}, ConnectionId: {connectionId}");
                    }
                }
                await Task.CompletedTask;
            };

            // New Handler
            _connection.On<string, string>("ReceivePrivateMessage", (fromUser, message) =>
            {
                Console.WriteLine($"Received private message from {fromUser}: {message}");
                OnPrivateMessageReceived?.Invoke(fromUser, message);
            });
        }

        /// <summary>
        ///   Initiates the SignalR hub connection with retry logic.
        /// </summary>
        /// <returns></returns>
        public async Task StartConnectionAsync()
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                Console.WriteLine($"Connection already connected, ConnectionId: {_connection.ConnectionId}");
                return;
            }
            if (_connection.State != HubConnectionState.Disconnected)
            {
                Console.WriteLine($"Connection in state {_connection.State}, attempting to stop before restart...");
                await _connection.StopAsync();
            }

            int retries = 3;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    Console.WriteLine($"Starting connection (attempt {i + 1})...");
                    await _connection.StartAsync();
                    OnConnected?.Invoke();
                    Console.WriteLine($"Client: Connected, ConnectionId: {_connection.ConnectionId}");
                    if (!string.IsNullOrEmpty(currentUser))
                    {
                        await JoinChat(currentUser); // Rejoin chat if user was set
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection attempt {i + 1} failed: {ex.Message}");
                    OnError?.Invoke($"Connection failed: {ex.Message}");
                    if (i == retries - 1) throw;
                    await Task.Delay(1000 * (i + 1));
                }
            }
        }

        public async Task SendMessage(string user, string message)
        {
            try
            {
                await _connection.InvokeAsync("SendMessage", user, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendMessage failed: {ex.Message}");
                OnError?.Invoke($"Failed to send message: {ex.Message}");
            }
        }

        public async Task JoinChat(string user)
        {
            try
            {
                Console.WriteLine($"Attempting JoinChat for {user}, ConnectionId: {_connection.ConnectionId}");
                currentUser = user;
                await _connection.InvokeAsync("JoinChat", user);
                Console.WriteLine($"Client: Joined chat as {user}, ConnectionId: {_connection.ConnectionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JoinChat failed: {ex.Message}");
                OnError?.Invoke($"Failed to join chat: {ex.Message}");
            }
        }

        public async Task LeaveChat(string user)
        {
            try
            {
                await _connection.InvokeAsync("LeaveChat", user);
                currentUser = "";
                joinedGroups.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LeaveChat failed: {ex.Message}");
                OnError?.Invoke($"Failed to leave chat: {ex.Message}");
            }
        }

        public async Task JoinGroup(string groupName)
        {
            try
            {
                Console.WriteLine($"Client: Joining group {groupName}, ConnectionId: {_connection.ConnectionId}, CurrentUser: {currentUser}");
                await _connection.InvokeAsync("JoinGroup", groupName);
                if (!joinedGroups.Contains(groupName))
                {
                    joinedGroups.Add(groupName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JoinGroup failed: {ex.Message}");
                OnError?.Invoke($"Failed to join group: {ex.Message}");
            }
        }

        public async Task LeaveGroup(string groupName)
        {
            try
            {
                Console.WriteLine($"Client: Leaving group {groupName}, ConnectionId: {_connection.ConnectionId}, CurrentUser: {currentUser}");
                await _connection.InvokeAsync("LeaveGroup", groupName);
                joinedGroups.Remove(groupName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LeaveGroup failed: {ex.Message}");
                OnError?.Invoke($"Failed to leave group: {ex.Message}");
            }
        }

        public async Task SendGroupMessage(string groupName, string message)
        {
            try
            {
                Console.WriteLine($"Sending to group {groupName}: {message}");
                await _connection.InvokeAsync("SendGroupMessage", groupName, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendGroupMessage failed: {ex.Message}");
                OnError?.Invoke($"Failed to send group message: {ex.Message}");
            }
        }

        public async Task SendPrivateMessage(string toUser, string message)
        {
            try
            {
                Console.WriteLine($"Sending private message to {toUser}: {message}");
                await _connection.InvokeAsync("SendPrivateMessage", toUser, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendPrivateMessage failed: {ex.Message}");
                OnError?.Invoke($"Failed to send private message: {ex.Message}");
            }
        }

        public bool IsConnected => _connection.State == HubConnectionState.Connected;
        public string ConnectionId => _connection.ConnectionId ?? "Not connected";
    }
}
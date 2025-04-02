using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace SignalR.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _connectedUsers = new();

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
            await Clients.All.SendAsync("UpdateUserList", _connectedUsers.Values.ToList());
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connectedUsers.TryRemove(Context.ConnectionId, out string? username))
            {
                Console.WriteLine($"Disconnected: {Context.ConnectionId}, User: {username}");
                await Clients.All.SendAsync("UserLeft", username);
                await Clients.All.SendAsync("UpdateUserList", _connectedUsers.Values.ToList());
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string user, string message)
        {
            try
            {
                Console.WriteLine($"SendMessage: {user} - {message}");
                await Clients.All.SendAsync("ReceiveMessage", user, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMessage: {ex.Message}");
                throw;
            }
        }

        public async Task JoinChat(string user)
        {
            _connectedUsers[Context.ConnectionId] = user;
            Console.WriteLine($"JoinChat: {Context.ConnectionId} as {user}");
            await Clients.All.SendAsync("UserJoined", user);
            await Clients.All.SendAsync("UpdateUserList", _connectedUsers.Values.ToList());
        }

        public async Task LeaveChat(string user)
        {
            if (_connectedUsers.TryRemove(Context.ConnectionId, out _))
            {
                Console.WriteLine($"LeaveChat: {Context.ConnectionId}, User: {user}");
                await Clients.All.SendAsync("UserLeft", user);
                await Clients.All.SendAsync("UpdateUserList", _connectedUsers.Values.ToList());
            }
        }

        public async Task JoinGroup(string groupName)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                if (_connectedUsers.TryGetValue(Context.ConnectionId, out string? username))
                {
                    Console.WriteLine($"JoinGroup: {Context.ConnectionId} as {username} joined {groupName}");
                    await Clients.Group(groupName).SendAsync("GroupMessage", $"{username} joined group {groupName}");
                }
                else
                {
                    Console.WriteLine($"JoinGroup: {Context.ConnectionId} not found in _connectedUsers");
                    await Clients.Group(groupName).SendAsync("GroupMessage", $"Anonymous user joined group {groupName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in JoinGroup: {ex.Message}");
                throw;
            }
        }

        public async Task LeaveGroup(string groupName)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                if (_connectedUsers.TryGetValue(Context.ConnectionId, out string? username))
                {
                    Console.WriteLine($"LeaveGroup: {Context.ConnectionId} as {username} left {groupName}");
                    await Clients.Group(groupName).SendAsync("GroupMessage", $"{username} left group {groupName}"); // Added broadcast
                }
                else
                {
                    Console.WriteLine($"LeaveGroup: {Context.ConnectionId} not found in _connectedUsers");
                    await Clients.Group(groupName).SendAsync("GroupMessage", $"Anonymous user left group {groupName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LeaveGroup: {ex.Message}");
                throw;
            }
        }

        public async Task SendGroupMessage(string groupName, string message)
        {
            try
            {
                Console.WriteLine($"SendGroupMessage called for {groupName}, ConnectionId: {Context.ConnectionId}");
                if (_connectedUsers.TryGetValue(Context.ConnectionId, out string? username))
                {
                    Console.WriteLine($"SendGroupMessage: {username} to {groupName} - {message}");
                    await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", username, message);
                }
                else
                {
                    Console.WriteLine($"SendGroupMessage: {Context.ConnectionId} not in _connectedUsers");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendGroupMessage: {ex.Message}");
                throw;
            }
        }
    }
}
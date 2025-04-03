using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

// FYI -- Server Programs Needs the HUB. Do not put in the client project.
// ChatHub: Manages real-time chat functionality including general messages, group messages,
// private messages, and user/group state.

namespace SignalR.Hubs;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new();

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
        await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedUsers.TryRemove(Context.ConnectionId, out string? username))
        {
            Console.WriteLine($"Disconnected: {Context.ConnectionId}, User: {username}");
            if (exception != null)
            {
                Console.WriteLine($"Disconnect error: {exception.Message}");
            }
            await Clients.All.SendAsync("UserLeft", username);
            await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
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
        ConnectedUsers[Context.ConnectionId] = user;
        Console.WriteLine($"JoinChat: {Context.ConnectionId} as {user}");
        await Clients.All.SendAsync("UserJoined", user);
        await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
    }

    public async Task LeaveChat(string user)
    {
        if (ConnectedUsers.TryRemove(Context.ConnectionId, out _))
        {
            Console.WriteLine($"LeaveChat: {Context.ConnectionId}, User: {user}");
            await Clients.All.SendAsync("UserLeft", user);
            await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
        }
    }

    public async Task JoinGroup(string groupName)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? username))
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
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? username))
            {
                Console.WriteLine($"LeaveGroup: {Context.ConnectionId} as {username} left {groupName}");
                await Clients.Group(groupName).SendAsync("GroupMessage", $"{username} left group {groupName}");
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
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? username))
            {
                Console.WriteLine($"SendGroupMessage: {username} to {groupName} - {message}");
                await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", username, groupName, message);
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

    public async Task SendPrivateMessage(string toUser, string message)
    {
        try
        {
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? fromUser))
            {
                var toConnectionId = ConnectedUsers.FirstOrDefault(x => x.Value == toUser).Key;
                if (toConnectionId != null)
                {
                    Console.WriteLine($"SendPrivateMessage: {fromUser} to {toUser} - {message}");
                    await Clients.Client(toConnectionId).SendAsync("ReceivePrivateMessage", fromUser, message);
                    await Clients.Client(Context.ConnectionId).SendAsync("ReceivePrivateMessage", fromUser, message);
                }
                else
                {
                    Console.WriteLine($"SendPrivateMessage: User {toUser} not found");
                    await Clients.Client(Context.ConnectionId).SendAsync("ReceivePrivateMessage", "System", $"{toUser} is not online");
                }
            }
            else
            {
                Console.WriteLine($"SendPrivateMessage: Sender {Context.ConnectionId} not in _connectedUsers");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendPrivateMessage: {ex.Message}");
            throw;
        }
    }
}
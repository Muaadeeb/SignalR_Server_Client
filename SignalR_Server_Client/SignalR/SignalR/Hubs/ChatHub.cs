namespace SignalR.Hubs;

using Microsoft.AspNetCore.SignalR;

public class ChatHub : Hub
{

    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public async Task JoinChat(string user)
    {
        await Clients.All.SendAsync("UserJoined", user);
        // Optional: Add user to a group or track connected users
    }

    public async Task LeaveChat(string user)
    {
        await Clients.All.SendAsync("UserLeft", user);
        // Optional: Remove user from a group
    }

    // Override connection events (optional)
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        // Could notify all clients of the new connection
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Could notify all clients of the disconnection
        await base.OnDisconnectedAsync(exception);
    }
}
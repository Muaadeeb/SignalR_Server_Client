namespace SignalR.Client.Services.Interfaces;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IChatService
{
    // Events
    event Action<string, string>? OnMessageReceived;
    event Action<string, string>? OnGroupMessageReceived;
    event Action<string>? OnUserJoined;
    event Action<string>? OnUserLeft;
    event Action<List<string>>? OnUserListUpdated;
    event Action<string>? OnGroupMessage;
    event Action? OnConnected;
    event Action? OnDisconnected;
    event Action? OnReconnecting;
    event Action? OnReconnected;
    event Action<string>? OnError;

    // Properties
    IReadOnlyList<string> JoinedGroups { get; }
    bool IsConnected { get; }
    string ConnectionId { get; }

    // Methods
    Task SendMessage(string user, string message);
    Task JoinChat(string user);
    Task LeaveChat(string user);
    Task JoinGroup(string groupName);
    Task LeaveGroup(string groupName);
    Task SendGroupMessage(string groupName, string message);
}

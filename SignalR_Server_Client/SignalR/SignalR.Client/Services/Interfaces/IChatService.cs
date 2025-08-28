namespace SignalR.Client.Services.Interfaces;

using SignalR.Client.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;



public interface IChatService
{
    // Updated Events to use ChatMessageDto
    event Action<ChatMessageDto>? OnMessageReceived;
    event Action<ChatMessageDto>? OnGroupMessageReceived;
    event Action<ChatMessageDto>? OnPrivateMessageReceived;
    
    // Existing Events
    event Action<string>? OnUserJoined;
    event Action<string>? OnUserLeft;
    event Action<List<string>>? OnUserListUpdated;
    event Action<string>? OnGroupMessage;
    event Action? OnConnected;
    event Action? OnDisconnected;
    event Action? OnReconnecting;
    event Action? OnReconnected;
    event Action<string>? OnError;
    

    // Existing Properties
    IReadOnlyList<string> JoinedGroups { get; }
    bool IsConnected { get; }
    string ConnectionId { get; }

    // Updated Methods
    Task StartConnectionAsync();
    Task SendMessage(string user, string message);
    Task JoinChat(string user, string language = "en"); // Updated to include language parameter
    Task LeaveChat(string user);
    Task JoinGroup(string groupName);
    Task LeaveGroup(string groupName);
    Task SendGroupMessage(string groupName, string message);
    Task SendPrivateMessage(string toUser, string message);
}
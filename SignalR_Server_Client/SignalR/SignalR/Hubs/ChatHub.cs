// FYI -- Server Programs Needs the HUB. Do not put in the client project.
// ChatHub: Manages real-time chat functionality including general messages, group messages,
// private messages, and user/group state. Designed for scalability with concurrent user tracking
// and per-recipient message translation using an external API (DeepL) or AI (Azure Cognative Services).


using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SignalR.Client.Shared.Models;
using Azure;
using Azure.AI.TextAnalytics;

namespace SignalR.Hubs;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new(); // Stores ConnectionId -> Username
    private static readonly ConcurrentDictionary<string, string> UserLanguages = new(); // Stores ConnectionId -> Language code
    private static readonly ConcurrentDictionary<string, HashSet<string>> GroupMembers = new(); // Stores GroupName -> Set of ConnectionIds
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) }; // Shared client with timeout

    private readonly IConfiguration _configuration; // For reading app settings
    private readonly string _activeTranslator; // "DeepL" or "Azure" to switch translators
    private TextAnalyticsClient? _textAnalyticsClient;

    /// <summary>
    /// Initializes the ChatHub with configuration for translator selection.
    /// </summary>
    /// <param name="configuration">The application configuration instance.</param>
    public ChatHub(IConfiguration configuration)
    {
        _configuration = configuration;
        _activeTranslator = _configuration["Translator:Active"] ?? "DeepL"; // Default to DeepL if not set
        Console.WriteLine($"Active translator set to: {_activeTranslator} at {DateTime.Now}");
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Connected: {Context.ConnectionId} at {DateTime.Now}");
        await base.OnConnectedAsync();
        await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedUsers.TryRemove(Context.ConnectionId, out string? username))
        {
            UserLanguages.TryRemove(Context.ConnectionId, out _); // Clean up language
            foreach (var group in GroupMembers.Where(kvp => kvp.Value.Contains(Context.ConnectionId)).ToList())
            {
                group.Value.Remove(Context.ConnectionId);
                if (group.Value.Count == 0) GroupMembers.TryRemove(group.Key, out _); // Clean up empty groups
            }

            Console.WriteLine($"Disconnected: {Context.ConnectionId}, User: {username} at {DateTime.Now}");
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
            Console.WriteLine($"SendMessage: {user} - {message} at {DateTime.Now}");
            var sentiment = await AnalyzeSentimentAsync(message); // Call once

            foreach (var connId in ConnectedUsers.Keys)
            {
                var targetLang = UserLanguages.GetValueOrDefault(connId, "en");
                var translated = await TranslateAsync(message, targetLang);

                var payload = new ChatMessageDto
                {
                    User = user,
                    Message = translated,
                    Sentiment = sentiment.Sentiment,
                    Positive = sentiment.Positive,
                    Neutral = sentiment.Neutral,
                    Negative = sentiment.Negative
                };

                await Clients.Client(connId).SendAsync("ReceiveMessage", payload);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendMessage: {ex.Message} at {DateTime.Now}");
            throw;
        }
    }

    public async Task JoinChat(string user, string language)
    {
        ConnectedUsers[Context.ConnectionId] = user;
        UserLanguages[Context.ConnectionId] = language;
        Console.WriteLine($"JoinChat: {Context.ConnectionId} as {user} with language {language} at {DateTime.Now}");
        await Clients.All.SendAsync("UserJoined", user);
        await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
    }

    public async Task LeaveChat(string user)
    {
        if (ConnectedUsers.TryRemove(Context.ConnectionId, out _))
        {
            UserLanguages.TryRemove(Context.ConnectionId, out _);
            Console.WriteLine($"LeaveChat: {Context.ConnectionId}, User: {user} at {DateTime.Now}");
            await Clients.All.SendAsync("UserLeft", user);
            await Clients.All.SendAsync("UpdateUserList", ConnectedUsers.Values.ToList());
        }
    }

    public async Task JoinGroup(string groupName)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            GroupMembers.GetOrAdd(groupName, _ => new HashSet<string>()).Add(Context.ConnectionId);
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? username))
            {
                Console.WriteLine($"JoinGroup: {Context.ConnectionId} as {username} joined {groupName} at {DateTime.Now}");
                await Clients.Group(groupName).SendAsync("GroupMessage", $"{username} joined group {groupName}");
            }
            else
            {
                Console.WriteLine($"JoinGroup: {Context.ConnectionId} not found in ConnectedUsers at {DateTime.Now}");
                await Clients.Group(groupName).SendAsync("GroupMessage", $"Anonymous user joined group {groupName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in JoinGroup: {ex.Message} at {DateTime.Now}");
            throw;
        }
    }

    public async Task LeaveGroup(string groupName)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            if (GroupMembers.TryGetValue(groupName, out var members))
            {
                members.Remove(Context.ConnectionId);
                if (members.Count == 0) GroupMembers.TryRemove(groupName, out _);
            }
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? username))
            {
                Console.WriteLine($"LeaveGroup: {Context.ConnectionId} as {username} left {groupName} at {DateTime.Now}");
                await Clients.Group(groupName).SendAsync("GroupMessage", $"{username} left group {groupName}");
            }
            else
            {
                Console.WriteLine($"LeaveGroup: {Context.ConnectionId} not found in ConnectedUsers at {DateTime.Now}");
                await Clients.Group(groupName).SendAsync("GroupMessage", $"Anonymous user left group {groupName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LeaveGroup: {ex.Message} at {DateTime.Now}");
            throw;
        }
    }

    public async Task SendGroupMessage(string groupName, string message)
    {
        try
        {
            Console.WriteLine($"SendGroupMessage called for {groupName}, ConnectionId: {Context.ConnectionId} at {DateTime.Now}");
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out string? username))
            {
                Console.WriteLine($"SendGroupMessage: {username} to {groupName} - {message} at {DateTime.Now}");
                if (GroupMembers.TryGetValue(groupName, out var members))
                {
                    var sentiment = await AnalyzeSentimentAsync(message); // Call once per message

                    foreach (var connId in members)
                    {
                        var targetLang = UserLanguages.GetValueOrDefault(connId, "en");
                        var translated = await TranslateAsync(message, targetLang);

                        var payload = new ChatMessageDto
                        {
                            User = username,
                            Group = groupName,
                            Message = translated,
                            Sentiment = sentiment.Sentiment,
                            Positive = sentiment.Positive,
                            Neutral = sentiment.Neutral,
                            Negative = sentiment.Negative
                        };

                        await Clients.Client(connId).SendAsync("ReceiveGroupMessage", payload);
                    }
                }
                else
                {
                    Console.WriteLine($"SendGroupMessage: Group {groupName} has no members at {DateTime.Now}");
                }
            }
            else
            {
                Console.WriteLine($"SendGroupMessage: {Context.ConnectionId} not in ConnectedUsers at {DateTime.Now}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendGroupMessage: {ex.Message} at {DateTime.Now}");
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
                    var sentiment = await AnalyzeSentimentAsync(message); // Call once per message

                    // To recipient
                    var targetLang = UserLanguages.GetValueOrDefault(toConnectionId, "en");
                    var translated = await TranslateAsync(message, targetLang);

                    var payloadTo = new ChatMessageDto
                    {
                        User = fromUser,
                        Message = translated,
                        Sentiment = sentiment.Sentiment,
                        Positive = sentiment.Positive,
                        Neutral = sentiment.Neutral,
                        Negative = sentiment.Negative
                    };

                    await Clients.Client(toConnectionId).SendAsync("ReceivePrivateMessage", payloadTo);

                    // To sender (echo)
                    var senderLang = UserLanguages.GetValueOrDefault(Context.ConnectionId, "en");
                    var senderTranslated = (senderLang == targetLang) ? translated : await TranslateAsync(message, senderLang);

                    var payloadFrom = new ChatMessageDto
                    {
                        User = fromUser,
                        Message = senderTranslated,
                        Sentiment = sentiment.Sentiment,
                        Positive = sentiment.Positive,
                        Neutral = sentiment.Neutral,
                        Negative = sentiment.Negative
                    };

                    await Clients.Client(Context.ConnectionId).SendAsync("ReceivePrivateMessage", payloadFrom);
                }
                else
                {
                    Console.WriteLine($"SendPrivateMessage: User {toUser} not found at {DateTime.Now}");
                    await Clients.Client(Context.ConnectionId).SendAsync("ReceivePrivateMessage", new ChatMessageDto
                    {
                        User = "System",
                        Message = $"{toUser} is not online",
                        Sentiment = "unknown"
                    });
                }
            }
            else
            {
                Console.WriteLine($"SendPrivateMessage: Sender {Context.ConnectionId} not in ConnectedUsers at {DateTime.Now}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendPrivateMessage: {ex.Message} at {DateTime.Now}");
            throw;
        }
    }

    private async Task<string> TranslateAsync(string text, string targetLanguage)
    {
        try
        {
            if (_activeTranslator.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                var requestBody = new[]
                {
                    new { Text = text } // Array of objects, each with a Text property
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to={targetLanguage}");
                var apiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY"); // Securely fetched from environment
                var region = Environment.GetEnvironmentVariable("AZURE_REGION") ?? "eastus"; // Updated to eastus based on your correction
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine($"Azure API key not found in environment variables at {DateTime.Now}");
                    return text; // Fallback if key is missing
                }
                request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", region);
                request.Content = content;

                Console.WriteLine($"Sending Azure request: Text={text}, Target={targetLanguage} at {DateTime.Now}");
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Azure Response: Status={response.StatusCode} at {DateTime.Now}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Azure Response Body: {jsonResponse} at {DateTime.Now}");
                    var document = JsonDocument.Parse(jsonResponse);
                    var result = document.RootElement[0].GetProperty("translations")[0].GetProperty("text").GetString();
                    return result ?? text;
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Azure Error Details: {errorDetails} at {DateTime.Now}");
                }
            }
            else // Default to DeepL
            {
                var requestBody = new
                {
                    text = new[] { text },
                    target_lang = targetLanguage.ToUpper()
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate");
                var apiKey = Environment.GetEnvironmentVariable("DEEPL_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine($"DeepL API key not found in environment variables at {DateTime.Now}");
                    return text; // Fallback if key is missing
                }
                request.Headers.Add("Authorization", apiKey);
                request.Content = content;

                Console.WriteLine($"Sending DeepL request: Text={text}, Target={targetLanguage}, Key=*** at {DateTime.Now}");
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"DeepL Response: Status={response.StatusCode} at {DateTime.Now}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DeepL Response Body: {jsonResponse} at {DateTime.Now}");
                    var result = JsonDocument.Parse(jsonResponse).RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
                    return result?.Trim() ?? text;
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DeepL Error Details: {errorDetails} at {DateTime.Now}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Translation failed for text: {text}, Service: {_activeTranslator}, Error: {ex.Message} at {DateTime.Now}");
        }
        return text; // Fallback to original
    }

    private void EnsureTextAnalyticsClient()
    {
        if (_textAnalyticsClient == null)
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_ENDPOINT_COGSERVICE");
            var apiKey = Environment.GetEnvironmentVariable("AZURE_COGSERVICE_API_KEY");
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Azure Cognitive Service endpoint or API key not found.");
            }
            _textAnalyticsClient = new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }
    }

    private async Task<(string Sentiment, double Positive, double Neutral, double Negative)> AnalyzeSentimentAsync(string text)
    {
        try
        {
            EnsureTextAnalyticsClient();
            var response = await _textAnalyticsClient!.AnalyzeSentimentAsync(text, "en");
            var sentiment = response.Value.Sentiment.ToString().ToLowerInvariant();
            var scores = response.Value.ConfidenceScores;
            return (sentiment, scores.Positive, scores.Neutral, scores.Negative);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sentiment analysis failed for text: {text}, Error: {ex.Message} at {DateTime.Now}");
            return ("unknown", 0, 0, 0);
        }
    }

    //private async Task<(string Sentiment, double Positive, double Neutral, double Negative)> AnalyzeSentimentAsync(string text)
    //{
    //    try
    //    {
    //        var endpoint = Environment.GetEnvironmentVariable("AZURE_ENDPOINT_COGSERVICE")?.TrimEnd('/');
    //        var apiKey = Environment.GetEnvironmentVariable("AZURE_COGSERVICE_API_KEY");
    //        var region = Environment.GetEnvironmentVariable("AZURE_REGION") ?? "eastus";

    //        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
    //        {
    //            Console.WriteLine($"Azure Cognitive Service endpoint or API key not found at {DateTime.Now}");
    //            return ("unknown", 0, 0, 0);
    //        }

    //        var url = $"{endpoint}/text/analytics/v3.2/sentiment";
    //        var requestBody = new
    //        {
    //            documents = new[] 
    //            {
    //                new { id = "1", language = "en", text }
    //            }
    //        };

    //        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
    //        var request = new HttpRequestMessage(HttpMethod.Post, url);
    //        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
    //        request.Headers.Add("Ocp-Apim-Subscription-Region", region);
    //        request.Content = content;

    //        Console.WriteLine($"Sending Sentiment Analysis request: Text={text} at {DateTime.Now}");
    //        var response = await _httpClient.SendAsync(request);
    //        Console.WriteLine($"Sentiment Analysis Response: Status={response.StatusCode} at {DateTime.Now}");

    //        if (response.IsSuccessStatusCode)
    //        {
    //            var jsonResponse = await response.Content.ReadAsStringAsync();
    //            Console.WriteLine($"Sentiment Analysis Response Body: {jsonResponse} at {DateTime.Now}");
    //            using var document = JsonDocument.Parse(jsonResponse);
    //            var doc = document.RootElement.GetProperty("documents")[0];
    //            var sentiment = doc.GetProperty("sentiment").GetString() ?? "unknown";
    //            var confidence = doc.GetProperty("confidenceScores");
    //            double positive = confidence.GetProperty("positive").GetDouble();
    //            double neutral = confidence.GetProperty("neutral").GetDouble();
    //            double negative = confidence.GetProperty("negative").GetDouble();
    //            return (sentiment, positive, neutral, negative);
    //        }
    //        else
    //        {
    //            var errorDetails = await response.Content.ReadAsStringAsync();
    //            Console.WriteLine($"Sentiment Analysis Error Details: {errorDetails} at {DateTime.Now}");
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Sentiment analysis failed for text: {text}, Error: {ex.Message} at {DateTime.Now}");
    //    }
    //    return ("unknown", 0, 0, 0);
    //}
}
namespace SignalR.Client.Shared.Models;
public class ChatMessageDto
{
    public string User { get; set; } = "";
    public string? Group { get; set; }
    public string Message { get; set; } = "";
    public string Sentiment { get; set; } = "";
    public double Positive { get; set; }
    public double Neutral { get; set; }
    public double Negative { get; set; }
}
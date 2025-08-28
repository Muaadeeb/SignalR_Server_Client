namespace SignalR.Client.Shared.Models;

public class StatusEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; } = string.Empty;

    public StatusEntry() { }

    public StatusEntry(string message)
    {
        Timestamp = DateTime.Now;
        Message = message;
    }
}

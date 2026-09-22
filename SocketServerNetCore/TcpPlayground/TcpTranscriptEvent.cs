namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpTranscriptEvent
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Direction { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

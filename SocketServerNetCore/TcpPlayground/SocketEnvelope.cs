using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public sealed class SocketEnvelope
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string ClientId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public JsonElement? Payload { get; set; }

    public T? DeserializePayload<T>()
        => Payload is { } payload
            ? payload.Deserialize<T>(JsonLineSocketProtocol.SerializerOptions)
            : default;

    public static SocketEnvelope Create(string type, string clientId, object? payload = null, string? requestId = null)
        => new()
        {
            Type = type,
            ClientId = clientId,
            RequestId = requestId ?? Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, JsonLineSocketProtocol.SerializerOptions)
        };
}

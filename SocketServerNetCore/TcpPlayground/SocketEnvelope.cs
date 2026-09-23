using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public sealed class SocketEnvelope
{
    public string ProtocolVersion { get; set; } = CommandProtocol.ProtocolVersion;
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public JsonElement? Payload { get; set; }

    public T? DeserializePayload<T>()
        => Payload is { } payload
            ? payload.Deserialize<T>(JsonLineSocketProtocol.SerializerOptions)
            : default;

    public static SocketEnvelope Create(
        string type,
        string deviceId,
        object? payload = null,
        string? requestId = null,
        string? role = null,
        string? correlationId = null)
        => new()
        {
            ProtocolVersion = CommandProtocol.ProtocolVersion,
            Type = type,
            DeviceId = deviceId,
            ClientId = deviceId,
            Role = role ?? string.Empty,
            RequestId = requestId ?? Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, JsonLineSocketProtocol.SerializerOptions)
        };
}

using System.Text;
using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public static class JsonLineSocketProtocol
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Serialize(SocketEnvelope envelope)
        => JsonSerializer.Serialize(envelope, SerializerOptions);

    public static bool TryDeserialize(string line, out SocketEnvelope? envelope, out string? error)
    {
        try
        {
            envelope = JsonSerializer.Deserialize<SocketEnvelope>(line, SerializerOptions);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Type))
            {
                error = "Message type is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(envelope.RequestId))
            {
                envelope.RequestId = Guid.NewGuid().ToString("N");
            }

            envelope.TimestampUtc = envelope.TimestampUtc == default ? DateTimeOffset.UtcNow : envelope.TimestampUtc;
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            envelope = null;
            error = exception.Message;
            return false;
        }
    }

    public static StreamReader CreateReader(Stream stream)
        => new(stream, Encoding.UTF8, leaveOpen: true);

    public static StreamWriter CreateWriter(Stream stream)
        => new(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
}

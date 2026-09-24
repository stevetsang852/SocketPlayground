using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Payload;

/// <summary>
/// Playground TLS client aligned with Python/Node CanonicalTcpClient.
/// Does not implement legacy Socket.IO handlers.
/// </summary>
public sealed class CanonicalTcpClient : IDisposable
{
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "health-check", "refresh-config", "collect-diagnostics", "list-status", "ping-time", "custom-cmd"
    };

    private readonly string _host;
    private readonly int _port;
    private readonly string _deviceId;
    private readonly bool _allowUntrusted;
    private readonly HashSet<string> _seenCommandIds = new(StringComparer.OrdinalIgnoreCase);
    private TcpClient? _tcp;
    private SslStream? _ssl;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private string _role;

    public CanonicalTcpClient(string host, int port, string deviceId, string role, bool allowUntrusted = true)
    {
        _host = host;
        _port = port;
        _deviceId = deviceId;
        _role = role;
        _allowUntrusted = allowUntrusted;
    }

    public async Task<JsonElement> ConnectAndLoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, cancellationToken);
        _ssl = new SslStream(_tcp.GetStream(), false, (_, _, _, errors) => _allowUntrusted || errors == SslPolicyErrors.None);
        await _ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
        }, cancellationToken);
        _reader = new StreamReader(_ssl, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        _writer = new StreamWriter(_ssl, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" };

        return await SendAndWaitAsync("login", new
        {
            deviceId = _deviceId,
            username,
            password,
            role = _role
        }, type => type is "authenticated" or "error", cancellationToken);
    }

    public async Task ProcessCommandsAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var envelope = await ReadAsync(cancellationToken);
            if (envelope.GetProperty("type").GetString() != "admin-command")
            {
                continue;
            }

            var payload = envelope.GetProperty("payload");
            var commandId = payload.GetProperty("commandId").GetString() ?? string.Empty;
            var commandName = payload.GetProperty("commandName").GetString() ?? string.Empty;
            await WriteAsync("command-ack", new { commandId, status = "accepted" }, commandId, cancellationToken);
            var duplicate = !_seenCommandIds.Add(commandId);
            await WriteAsync("command-result", new
            {
                commandId,
                commandName,
                status = "completed",
                success = true,
                duplicate,
                result = Execute(commandName)
            }, commandId, cancellationToken);
        }
    }

    private object Execute(string commandName)
    {
        if (!AllowedCommands.Contains(commandName))
        {
            return new { status = "rejected", reason = "not allowlisted" };
        }

        return commandName switch
        {
            "health-check" => new { status = "ok", deviceId = _deviceId, observedAtUtc = DateTimeOffset.UtcNow },
            "refresh-config" => new { status = "refreshed", deviceId = _deviceId, appliedAtUtc = DateTimeOffset.UtcNow },
            "collect-diagnostics" => new { status = "collected", deviceId = _deviceId, diagnostics = new { pid = Environment.ProcessId } },
            "list-status" => new { status = "ready", deviceId = _deviceId, role = _role, observedAtUtc = DateTimeOffset.UtcNow },
            "ping-time" => new { status = "pong", deviceId = _deviceId, observedAtUtc = DateTimeOffset.UtcNow },
            "custom-cmd" => new { status = "acknowledged", executed = false, note = "allowlisted stub only; no shell or code execution", deviceId = _deviceId },
            _ => new { status = "rejected" }
        };
    }

    private async Task<JsonElement> SendAndWaitAsync(string type, object payload, Func<string?, bool> match, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        await WriteAsync(type, payload, null, cancellationToken, requestId);
        while (true)
        {
            var envelope = await ReadAsync(cancellationToken);
            if (envelope.TryGetProperty("requestId", out var id) && id.GetString() == requestId && match(envelope.GetProperty("type").GetString()))
            {
                if (envelope.GetProperty("type").GetString() == "error")
                {
                    throw new InvalidOperationException(envelope.GetProperty("payload").GetProperty("message").GetString());
                }
                return envelope;
            }
        }
    }

    private async Task WriteAsync(string type, object? payload, string? correlationId, CancellationToken cancellationToken, string? requestId = null)
    {
        if (_writer is null) throw new InvalidOperationException("Not connected.");
        var envelope = new Dictionary<string, object?>
        {
            ["protocolVersion"] = "1.0",
            ["requestId"] = requestId ?? Guid.NewGuid().ToString("N"),
            ["deviceId"] = _deviceId,
            ["clientId"] = _deviceId,
            ["role"] = _role,
            ["type"] = type,
            ["correlationId"] = correlationId,
            ["timestampUtc"] = DateTimeOffset.UtcNow,
            ["payload"] = payload
        };
        await _writer.WriteLineAsync(JsonSerializer.Serialize(envelope));
    }

    private async Task<JsonElement> ReadAsync(CancellationToken cancellationToken)
    {
        if (_reader is null) throw new InvalidOperationException("Not connected.");
        var line = await _reader.ReadLineAsync(cancellationToken);
        if (line is null) throw new IOException("Disconnected.");
        return JsonSerializer.Deserialize<JsonElement>(line);
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _ssl?.Dispose();
        _tcp?.Dispose();
    }
}

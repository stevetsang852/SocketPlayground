using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;

namespace Payload;

/// <summary>
/// Playground TLS client aligned with Python/Node CanonicalTcpClient.
/// Dispatches allowlisted playground commands and, when the server enables them,
/// bridged legacy Socket.IO handlers (csharp / upload / wallpapertaskpack).
/// </summary>
public sealed class CanonicalTcpClient : IDisposable
{
    /// <summary>Safe defaults before login; replaced by the server commandAllowlist after auth.</summary>
    private static readonly HashSet<string> SafeDefaultCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "health-check",
        "refresh-config",
        "collect-diagnostics",
        "list-status",
        "ping-time",
        "custom-cmd"
    };

    private readonly LegacyCommandBridge _legacy = new();
    private readonly string _host;
    private readonly int _port;
    private readonly string _deviceId;
    private readonly bool _allowUntrusted;
    private readonly HashSet<string> _seenCommandIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _allowedCommands = new(SafeDefaultCommands, StringComparer.OrdinalIgnoreCase);
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

    public IReadOnlySet<string> AllowedCommandNames => _allowedCommands;

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

        var authenticated = await SendAndWaitAsync("login", new
        {
            deviceId = _deviceId,
            username,
            password,
            role = _role
        }, type => type is "authenticated" or "error", cancellationToken);

        ApplyServerAllowlist(authenticated);
        return authenticated;
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
            JsonElement? arguments = null;
            if (payload.TryGetProperty("arguments", out var argsElement)
                && argsElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                arguments = argsElement;
            }

            await WriteAsync("command-ack", new { commandId, status = "accepted" }, commandId, cancellationToken);
            var duplicate = !_seenCommandIds.Add(commandId);
            object result;
            var success = true;
            try
            {
                result = Execute(commandName, arguments);
                if (result is { } obj)
                {
                    var json = JsonSerializer.SerializeToElement(obj);
                    if (json.ValueKind == JsonValueKind.Object
                        && json.TryGetProperty("status", out var status)
                        && string.Equals(status.GetString(), "rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;
                result = new { status = "error", message = ex.Message };
            }

            await WriteAsync("command-result", new
            {
                commandId,
                commandName,
                status = success ? "completed" : "failed",
                success,
                duplicate,
                result
            }, commandId, cancellationToken);
        }
    }

    /// <summary>
    /// Allowlist-only command execution used by the TLS agent loop and unit tests.
    /// After login, the allowlist mirrors the server's commandAllowlist.
    /// </summary>
    public object Execute(string commandName, JsonElement? arguments = null)
    {
        if (!_allowedCommands.Contains(commandName))
        {
            return new { status = "rejected", reason = "not allowlisted" };
        }

        if (LegacyCommandBridge.LegacyCommandNames.Contains(commandName))
        {
            return _legacy.Handle(commandName, arguments);
        }

        return commandName.ToLowerInvariant() switch
        {
            "health-check" => new { status = "ok", deviceId = _deviceId, observedAtUtc = DateTimeOffset.UtcNow },
            "refresh-config" => new { status = "refreshed", deviceId = _deviceId, appliedAtUtc = DateTimeOffset.UtcNow },
            "collect-diagnostics" => new { status = "collected", deviceId = _deviceId, diagnostics = new { pid = Environment.ProcessId } },
            "list-status" => new { status = "ready", deviceId = _deviceId, role = _role, observedAtUtc = DateTimeOffset.UtcNow },
            "ping-time" => new { status = "pong", deviceId = _deviceId, observedAtUtc = DateTimeOffset.UtcNow },
            "custom-cmd" => new { status = "acknowledged", executed = false, note = "allowlisted stub only; no shell or code execution", deviceId = _deviceId },
            _ => new { status = "rejected", reason = "not allowlisted" }
        };
    }

    private void ApplyServerAllowlist(JsonElement authenticated)
    {
        if (!authenticated.TryGetProperty("payload", out var payload)
            || !payload.TryGetProperty("commandAllowlist", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list.EnumerateArray())
        {
            var name = item.GetString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                next.Add(name);
            }
        }

        if (next.Count > 0)
        {
            _allowedCommands = next;
        }
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

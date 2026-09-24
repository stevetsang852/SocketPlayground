using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpPlaygroundServer : IAsyncDisposable
{
    private readonly TcpPlaygroundServerOptions _options;
    private readonly ConcurrentDictionary<string, ServerClientConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ServerClientConnection> _authenticatedConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PendingCommandState> _pendingCommands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> _connectionTasks = new();
    private readonly object _taskLock = new();
    private CancellationTokenSource? _lifetimeCts;
    private TcpListener? _listener;
    private Task? _acceptLoop;
    public TcpPlaygroundServer(TcpPlaygroundServerOptions options) { _options = options; }
    public int Port { get; private set; }
    public IReadOnlyList<AuthenticatedDeviceInfo> GetAuthenticatedDevices()
        => _authenticatedConnections.Values.Where(c => c.IsAuthenticated).Select(c => new AuthenticatedDeviceInfo(c.DeviceId, c.Role, c.LastHeartbeatUtc, c.AuthenticatedUntilUtc)).OrderBy(d => d.DeviceId, StringComparer.OrdinalIgnoreCase).ToArray();
    public Task DispatchConsoleCommandAsync(string commandName, string targetMode = "all", IEnumerable<string>? targetDeviceIds = null, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        var envelope = SocketEnvelope.Create("admin-command", CommandProtocol.ServerDeviceId, new AdminCommandRequest { CommandId = Guid.NewGuid().ToString("N"), CommandName = commandName, TargetMode = targetMode, TargetDeviceIds = targetDeviceIds?.ToArray(), TimeoutMs = timeoutMs }, role: DeviceRoles.Admin);
        return HandleAdminCommandAsync(CreateConsoleAdminConnection(), envelope, cancellationToken);
    }
    private ServerClientConnection CreateConsoleAdminConnection() => new(new TcpClient()) { DeviceId = CommandProtocol.ServerDeviceId, Role = DeviceRoles.Admin, IsAuthenticated = true, AuthenticatedUntilUtc = DateTimeOffset.MaxValue, IsConsoleAdmin = true };
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null) throw new InvalidOperationException("The server is already running.");
        if (_options.ServerCertificate is null) throw new InvalidOperationException("TLS server certificate is required.");
        if (string.IsNullOrWhiteSpace(_options.AuthenticationSecret)) throw new InvalidOperationException("Authentication secret is required.");
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Loopback, _options.Port);
        _listener.Start(_options.Backlog);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        LogAudit("server.started", $"Listening on 127.0.0.1:{Port} with {CommandProtocol.ProtocolVersion}.");
        _acceptLoop = AcceptLoopAsync(_lifetimeCts.Token);
        await Task.Yield();
    }
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is null) return;
        _lifetimeCts?.Cancel();
        _listener.Stop();
        foreach (var pending in _pendingCommands.Values) pending.Dispose();
        foreach (var connection in _connections.Values) connection.Dispose();
        if (_acceptLoop is not null) await AwaitAndIgnoreSocketShutdownAsync(_acceptLoop);
        Task[] tasks; lock (_taskLock) { tasks = _connectionTasks.ToArray(); }
        await Task.WhenAll(tasks.Select(AwaitAndIgnoreSocketShutdownAsync));
        _connections.Clear(); _authenticatedConnections.Clear(); _pendingCommands.Clear();
        _acceptLoop = null; _listener = null; _lifetimeCts?.Dispose(); _lifetimeCts = null; Port = 0;
    }
    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }
            var connection = new ServerClientConnection(client);
            _connections[connection.ConnectionId] = connection;
            var task = HandleClientAsync(connection, cancellationToken);
            lock (_taskLock) { _connectionTasks.Add(task); }
            _ = task.ContinueWith(_ => { lock (_taskLock) { _connectionTasks.Remove(task); } }, TaskScheduler.Default);
        }
    }
    private async Task HandleClientAsync(ServerClientConnection connection, CancellationToken cancellationToken)
    {
        using var stream = await OpenStreamAsync(connection.Client, cancellationToken);
        using var reader = JsonLineSocketProtocol.CreateReader(stream);
        using var writer = JsonLineSocketProtocol.CreateWriter(stream);
        connection.Writer = writer;
        try
        {
            if (!await AuthenticateConnectionAsync(connection, reader, cancellationToken)) return;
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;
                if (!JsonLineSocketProtocol.TryDeserialize(line, out var envelope, out var error))
                {
                    await SendErrorAsync(connection, "malformed_frame", "Malformed JSON line rejected.", error, cancellationToken);
                    break;
                }
                if (connection.AuthenticatedUntilUtc <= DateTimeOffset.UtcNow)
                {
                    await SendErrorAsync(connection, "token_expired", "Authentication token has expired.", null, cancellationToken);
                    break;
                }
                await HandleMessageAsync(connection, envelope!, cancellationToken);
            }
        }
        catch (AuthenticationException) { }
        catch (IOException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            _connections.TryRemove(connection.ConnectionId, out _);
            RemoveAuthenticatedConnection(connection);
            connection.Dispose();
            LogAudit("connection.closed", $"Connection {connection.ConnectionId} for device '{connection.DeviceIdOrConnectionId}' closed.");
        }
    }
    private async Task<bool> AuthenticateConnectionAsync(ServerClientConnection connection, StreamReader reader, CancellationToken cancellationToken)
    {
        using var authTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        authTimeout.CancelAfter(_options.AuthenticationTimeout);
        string? line;
        try { line = await reader.ReadLineAsync(authTimeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await SendErrorAsync(connection, "auth_timeout", "Authentication deadline exceeded.", null, CancellationToken.None);
            return false;
        }
        if (line is null) return false;
        if (!JsonLineSocketProtocol.TryDeserialize(line, out var envelope, out var error))
        {
            await SendErrorAsync(connection, "malformed_frame", "Malformed JSON line rejected.", error, CancellationToken.None);
            return false;
        }
        if (string.Equals(envelope!.Type, "login", StringComparison.Ordinal))
            return await CompleteLoginAsync(connection, envelope, CancellationToken.None);
        if (!string.Equals(envelope.Type, "authenticate", StringComparison.Ordinal))
        {
            await SendErrorAsync(connection, "auth_required", "The first message must be authenticate or login.", null, CancellationToken.None);
            return false;
        }
        var authenticateRequest = envelope.DeserializePayload<AuthenticateRequest>();
        if (authenticateRequest is null)
        {
            await SendErrorAsync(connection, "auth_invalid", "Authentication payload is required.", null, CancellationToken.None);
            return false;
        }
        if (!CommandAuthTokenService.TryValidateToken(authenticateRequest.AccessToken ?? string.Empty, _options.AuthenticationSecret, out var descriptor, out var tokenError))
        {
            await SendErrorAsync(connection, "auth_invalid", "Authentication failed.", tokenError, CancellationToken.None, envelope.RequestId);
            return false;
        }
        if (!string.IsNullOrWhiteSpace(authenticateRequest.DeviceId) && !string.Equals(authenticateRequest.DeviceId, descriptor!.DeviceId, StringComparison.Ordinal))
        {
            await SendErrorAsync(connection, "auth_invalid", "Authentication deviceId mismatch.", null, CancellationToken.None, envelope.RequestId);
            return false;
        }
        connection.DeviceId = descriptor!.DeviceId;
        connection.Role = descriptor.Role;
        connection.AuthenticatedUntilUtc = descriptor.ExpiresAtUtc;
        connection.IsAuthenticated = true;
        if (!TryRegisterAuthenticatedConnection(connection, out var duplicateError))
        {
            await SendErrorAsync(connection, "duplicate_device", duplicateError ?? "Duplicate device rejected.", null, CancellationToken.None, envelope.RequestId);
            return false;
        }
        LogAudit("auth.accepted", $"Device '{connection.DeviceId}' authenticated as {connection.Role} until {connection.AuthenticatedUntilUtc:O}.");
        await connection.SendAsync(SocketEnvelope.Create("authenticated", CommandProtocol.ServerDeviceId, new { deviceId = connection.DeviceId, role = connection.Role, duplicatePolicy = _options.DuplicateSessionPolicy.ToString(), authenticatedUntilUtc = connection.AuthenticatedUntilUtc, protocolVersion = CommandProtocol.ProtocolVersion, commandAllowlist = CommandProtocol.AllowedCommands.OrderBy(c => c).ToArray() }, envelope.RequestId, CommandProtocol.ServerRole), CancellationToken.None);
        return true;
    }
    private async Task<bool> CompleteLoginAsync(ServerClientConnection connection, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        var request = envelope.DeserializePayload<LoginRequest>();
        if (request is null)
        {
            await SendErrorAsync(connection, "login_invalid", "Login payload is required.", null, cancellationToken, envelope.RequestId);
            return false;
        }
        if (!LoginService.TryAuthenticate(request, _options.AuthenticationSecret, _options.AdminUsername, _options.AdminPassword, out var deviceId, out var role, out var error))
        {
            await SendErrorAsync(connection, "login_invalid", "Login failed.", error, cancellationToken, envelope.RequestId);
            return false;
        }
        var wasAuthenticated = connection.IsAuthenticated;
        if (wasAuthenticated && !string.Equals(connection.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            await SendErrorAsync(connection, "login_invalid", "Login deviceId does not match the current session.", null, cancellationToken, envelope.RequestId);
            return false;
        }
        connection.DeviceId = deviceId;
        connection.Role = role;
        connection.AuthenticatedUntilUtc = DateTimeOffset.UtcNow.AddHours(1);
        connection.IsAuthenticated = true;
        if (!wasAuthenticated && !TryRegisterAuthenticatedConnection(connection, out var duplicateError))
        {
            await SendErrorAsync(connection, "duplicate_device", duplicateError ?? "Duplicate device rejected.", null, cancellationToken, envelope.RequestId);
            return false;
        }
        LogAudit("login.accepted", $"Device '{connection.DeviceId}' logged in as {connection.Role} until {connection.AuthenticatedUntilUtc:O}.");
        await connection.SendAsync(SocketEnvelope.Create("authenticated", CommandProtocol.ServerDeviceId, new { deviceId = connection.DeviceId, role = connection.Role, method = "login", duplicatePolicy = _options.DuplicateSessionPolicy.ToString(), authenticatedUntilUtc = connection.AuthenticatedUntilUtc, protocolVersion = CommandProtocol.ProtocolVersion, commandAllowlist = CommandProtocol.AllowedCommands.OrderBy(c => c).ToArray() }, envelope.RequestId, CommandProtocol.ServerRole), cancellationToken);
        return true;
    }
    private bool TryRegisterAuthenticatedConnection(ServerClientConnection connection, out string? error)
    {
        error = null;
        while (true)
        {
            if (!_authenticatedConnections.TryGetValue(connection.DeviceId, out var existing))
            {
                if (_authenticatedConnections.TryAdd(connection.DeviceId, connection)) return true;
                continue;
            }
            if (ReferenceEquals(existing, connection)) return true;
            if (_options.DuplicateSessionPolicy == DuplicateSessionPolicy.ReplaceExisting)
            {
                if (_authenticatedConnections.TryUpdate(connection.DeviceId, connection, existing))
                {
                    LogAudit("duplicate.replace", $"Replacing authenticated session for device '{connection.DeviceId}'.");
                    existing.Dispose();
                    return true;
                }
                continue;
            }
            error = $"Device '{connection.DeviceId}' already has an authenticated session.";
            LogAudit("duplicate.reject", error);
            return false;
        }
    }
    private void RemoveAuthenticatedConnection(ServerClientConnection connection)
    {
        if (!connection.IsAuthenticated || string.IsNullOrWhiteSpace(connection.DeviceId)) return;
        if (_authenticatedConnections.TryGetValue(connection.DeviceId, out var current) && ReferenceEquals(current, connection))
            _authenticatedConnections.TryRemove(connection.DeviceId, out _);
    }
    private async Task<Stream> OpenStreamAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        var sslStream = new SslStream(stream, leaveInnerStreamOpen: false, userCertificateValidationCallback: (_, certificate, chain, errors) =>
        {
            var inspection = CertificateInspector.Inspect(certificate);
            if (!inspection.HasCertificate)
            {
                LogAudit("tls.client-cert", "No client certificate presented.");
                if (_options.RequireClientCertificate) { LogAudit("tls.reject", "Client certificate is required."); return false; }
                return true;
            }
            LogAudit("tls.client-cert", $"subject={inspection.Subject}; thumbprint={inspection.Thumbprint}; expires={inspection.NotAfterUtc:O}");
            if (!CertificateInspector.TryValidateServerCertificate(certificate, chain, errors, !_options.RequireValidClientCertificate, out var error))
            {
                LogAudit("tls.reject", error ?? "Client certificate rejected.");
                return false;
            }
            return true;
        });
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _options.ServerCertificate, ClientCertificateRequired = _options.RequireClientCertificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13, CertificateRevocationCheckMode = X509RevocationMode.NoCheck }, cancellationToken);
        var serverCert = CertificateInspector.Inspect(_options.ServerCertificate);
        LogAudit("tls.accepted", $"Server certificate thumbprint={serverCert.Thumbprint}; subject={serverCert.Subject}.");
        return sslStream;
    }
    private async Task HandleMessageAsync(ServerClientConnection connection, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.Type)
        {
            case "heartbeat":
                connection.LastHeartbeatUtc = DateTimeOffset.UtcNow;
                await connection.SendAsync(SocketEnvelope.Create("heartbeat.ack", CommandProtocol.ServerDeviceId, new { deviceId = connection.DeviceId, receivedAtUtc = DateTimeOffset.UtcNow }, envelope.RequestId, CommandProtocol.ServerRole), cancellationToken);
                break;
            case "echo":
                await connection.SendAsync(SocketEnvelope.Create("echo.response", connection.DeviceId, envelope.Payload, envelope.RequestId, connection.Role), cancellationToken);
                break;
            case "login":
                await CompleteLoginAsync(connection, envelope, cancellationToken);
                break;
            case "admin-command":
                await HandleAdminCommandAsync(connection, envelope, cancellationToken);
                break;
            case "command-ack":
                await HandleCommandAckAsync(connection, envelope, cancellationToken);
                break;
            case "command-result":
                await HandleCommandResultAsync(connection, envelope, cancellationToken);
                break;
            default:
                await SendErrorAsync(connection, "unsupported_type", $"Unsupported message type '{envelope.Type}'.", null, cancellationToken, envelope.RequestId);
                break;
        }
    }
    private async Task HandleAdminCommandAsync(ServerClientConnection connection, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!string.Equals(connection.Role, DeviceRoles.Admin, StringComparison.Ordinal))
        {
            await SendErrorAsync(connection, "forbidden", "Only admin-role clients may submit admin commands.", null, cancellationToken, envelope.RequestId);
            return;
        }
        var request = envelope.DeserializePayload<AdminCommandRequest>();
        if (request is null) { await SendErrorAsync(connection, "command_invalid", "Admin command payload is required.", null, cancellationToken, envelope.RequestId); return; }
        var commandName = request.CommandName?.Trim();
        if (!CommandProtocol.AllowedCommands.Contains(commandName ?? string.Empty))
        {
            await SendErrorAsync(connection, "command_not_allowed", $"Command '{commandName}' is not allowlisted.", null, cancellationToken, envelope.RequestId);
            return;
        }
        var targets = ResolveTargets(connection, request);
        if (targets.Count == 0)
        {
            await SendErrorAsync(connection, "target_unavailable", "No eligible authenticated clients matched the target selection.", null, cancellationToken, envelope.RequestId);
            return;
        }
        var commandId = string.IsNullOrWhiteSpace(request.CommandId) ? Guid.NewGuid().ToString("N") : request.CommandId;
        var timeoutMs = Math.Clamp(request.TimeoutMs.GetValueOrDefault((int)_options.DefaultCommandTimeout.TotalMilliseconds), 250, 30000);
        var pending = new PendingCommandState(commandId, envelope.RequestId, commandName!, connection, targets.Select(t => t.DeviceId), timeoutMs);
        if (!_pendingCommands.TryAdd(commandId, pending))
        {
            await SendErrorAsync(connection, "command_duplicate", $"A command with id '{commandId}' is already active.", null, cancellationToken, envelope.RequestId);
            pending.Dispose();
            return;
        }
        LogAudit("command.accepted", $"Admin '{connection.DeviceId}' dispatched '{commandName}' to [{string.Join(", ", targets.Select(t => t.DeviceId))}] with commandId '{commandId}'.");
        _ = MonitorPendingCommandTimeoutAsync(pending);
        await connection.SendAsync(SocketEnvelope.Create("admin-command.accepted", CommandProtocol.ServerDeviceId, new { commandId, commandName, targetDeviceIds = targets.Select(t => t.DeviceId).ToArray(), timeoutMs }, envelope.RequestId, CommandProtocol.ServerRole, commandId), cancellationToken);
        foreach (var target in targets)
            await target.SendAsync(SocketEnvelope.Create("admin-command", connection.DeviceId, new { commandId, commandName, arguments = request.Arguments, issuedByDeviceId = connection.DeviceId, timeoutMs }, role: connection.Role, correlationId: commandId), cancellationToken);
    }
    private IReadOnlyList<ServerClientConnection> ResolveTargets(ServerClientConnection adminConnection, AdminCommandRequest request)
    {
        if (string.Equals(request.TargetMode, "devices", StringComparison.OrdinalIgnoreCase))
        {
            var ids = request.TargetDeviceIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
            return ids.Select(id => _authenticatedConnections.TryGetValue(id, out var c) ? c : null).Where(c => c is not null && !string.Equals(c.Role, DeviceRoles.Admin, StringComparison.Ordinal) && !ReferenceEquals(c, adminConnection)).Cast<ServerClientConnection>().ToArray();
        }
        return _authenticatedConnections.Values.Where(c => !ReferenceEquals(c, adminConnection) && string.Equals(c.Role, DeviceRoles.Client, StringComparison.Ordinal)).OrderBy(c => c.DeviceId, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private async Task HandleCommandAckAsync(ServerClientConnection connection, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.DeserializePayload<CommandAckPayload>();
        var commandId = payload?.CommandId ?? envelope.CorrelationId;
        if (string.IsNullOrWhiteSpace(commandId) || !_pendingCommands.TryGetValue(commandId, out var pending))
        {
            await SendErrorAsync(connection, "command_unknown", "Command acknowledgement does not match an active command.", null, cancellationToken, envelope.RequestId);
            return;
        }
        if (!pending.TryRecordAck(connection.DeviceId)) return;
        LogAudit("command.ack", $"Device '{connection.DeviceId}' acknowledged command '{commandId}'.");
        await NotifyAdminAsync(pending, SocketEnvelope.Create("command-ack", connection.DeviceId, new { commandId, commandName = pending.CommandName, status = payload?.Status ?? "accepted", duplicate = payload?.Duplicate ?? false }, role: connection.Role, correlationId: commandId), cancellationToken);
    }
    private async Task HandleCommandResultAsync(ServerClientConnection connection, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.DeserializePayload<CommandResultPayload>();
        var commandId = payload?.CommandId ?? envelope.CorrelationId;
        if (string.IsNullOrWhiteSpace(commandId) || !_pendingCommands.TryGetValue(commandId, out var pending))
        {
            await SendErrorAsync(connection, "command_unknown", "Command result does not match an active command.", null, cancellationToken, envelope.RequestId);
            return;
        }
        if (!pending.TryRecordResult(connection.DeviceId)) return;
        LogAudit("command.result", $"Device '{connection.DeviceId}' completed command '{commandId}' with status '{payload?.Status ?? "completed"}'.");
        await NotifyAdminAsync(pending, SocketEnvelope.Create("command-result", connection.DeviceId, new { commandId, commandName = pending.CommandName, status = payload?.Status ?? "completed", success = payload?.Success ?? true, duplicate = payload?.Duplicate ?? false, result = payload?.Result }, role: connection.Role, correlationId: commandId), cancellationToken);
        if (pending.IsComplete) await FinalizePendingCommandAsync(commandId, false, cancellationToken);
    }
    private async Task MonitorPendingCommandTimeoutAsync(PendingCommandState pending)
    {
        try { await Task.Delay(pending.Timeout, pending.Cancellation.Token); if (!pending.Cancellation.IsCancellationRequested) await FinalizePendingCommandAsync(pending.CommandId, true, CancellationToken.None); }
        catch (OperationCanceledException) { }
    }
    private async Task FinalizePendingCommandAsync(string commandId, bool timedOut, CancellationToken cancellationToken)
    {
        if (!_pendingCommands.TryRemove(commandId, out var pending)) return;
        pending.Cancellation.Cancel();
        var summary = pending.CreateSummary(timedOut);
        LogAudit("command.summary", $"Command '{commandId}' summary => completed [{string.Join(", ", summary.CompletedDeviceIds)}], timed out [{string.Join(", ", summary.TimedOutDeviceIds)}].");
        await NotifyAdminAsync(pending, SocketEnvelope.Create("command-summary", CommandProtocol.ServerDeviceId, summary, pending.RequestId, CommandProtocol.ServerRole, commandId), cancellationToken);
        pending.Dispose();
    }
    private async Task NotifyAdminAsync(PendingCommandState pending, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        if (pending.AdminConnection.IsConsoleAdmin) { Console.WriteLine(JsonLineSocketProtocol.Serialize(envelope)); return; }
        await pending.AdminConnection.SendAsync(envelope, cancellationToken);
    }
    private async Task SendErrorAsync(ServerClientConnection connection, string code, string message, string? detail, CancellationToken cancellationToken, string? requestId = null)
    {
        LogAudit("error", $"{code}: {message}{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" ({detail})")}");
        await connection.SendAsync(SocketEnvelope.Create("error", CommandProtocol.ServerDeviceId, new { code, message, detail }, requestId, CommandProtocol.ServerRole), cancellationToken);
    }
    private static void LogAudit(string eventType, string message) => Console.WriteLine($"[{DateTimeOffset.UtcNow:O}] {eventType} {message}");
    private static async Task AwaitAndIgnoreSocketShutdownAsync(Task task)
    {
        try { await task; } catch (OperationCanceledException) { } catch (IOException) { } catch (ObjectDisposedException) { } catch (SocketException) { } catch (AuthenticationException) { }
    }
    public async ValueTask DisposeAsync() => await StopAsync();
    private sealed class AuthenticateRequest { public string? DeviceId { get; set; } public string? AccessToken { get; set; } }
    private sealed class AdminCommandRequest { public string? CommandId { get; set; } public string? CommandName { get; set; } public JsonElement? Arguments { get; set; } public string? TargetMode { get; set; } = "all"; public string[]? TargetDeviceIds { get; set; } public int? TimeoutMs { get; set; } }
    private sealed class CommandAckPayload { public string? CommandId { get; set; } public string? Status { get; set; } public bool Duplicate { get; set; } }
    private sealed class CommandResultPayload { public string? CommandId { get; set; } public string? Status { get; set; } public bool Success { get; set; } = true; public bool Duplicate { get; set; } public JsonElement? Result { get; set; } }
    private sealed class CommandSummary { public string CommandId { get; init; } = string.Empty; public string CommandName { get; init; } = string.Empty; public string[] TargetDeviceIds { get; init; } = Array.Empty<string>(); public string[] AcknowledgedDeviceIds { get; init; } = Array.Empty<string>(); public string[] CompletedDeviceIds { get; init; } = Array.Empty<string>(); public string[] TimedOutDeviceIds { get; init; } = Array.Empty<string>(); public int TimeoutMs { get; init; } public bool TimedOut { get; init; } }
    private sealed class PendingCommandState : IDisposable
    {
        private readonly object _sync = new();
        private readonly HashSet<string> _targetDeviceIds;
        private readonly HashSet<string> _acknowledgedDeviceIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _completedDeviceIds = new(StringComparer.OrdinalIgnoreCase);
        public PendingCommandState(string commandId, string requestId, string commandName, ServerClientConnection adminConnection, IEnumerable<string> targetDeviceIds, int timeoutMs)
        {
            CommandId = commandId; RequestId = requestId; CommandName = commandName; AdminConnection = adminConnection;
            Timeout = TimeSpan.FromMilliseconds(timeoutMs); TimeoutMs = timeoutMs; Cancellation = new CancellationTokenSource();
            _targetDeviceIds = new HashSet<string>(targetDeviceIds, StringComparer.OrdinalIgnoreCase);
        }
        public string CommandId { get; }
        public string RequestId { get; }
        public string CommandName { get; }
        public ServerClientConnection AdminConnection { get; }
        public TimeSpan Timeout { get; }
        public int TimeoutMs { get; }
        public CancellationTokenSource Cancellation { get; }
        public bool IsComplete { get { lock (_sync) { return _completedDeviceIds.Count == _targetDeviceIds.Count; } } }
        public bool TryRecordAck(string deviceId) { lock (_sync) { return _targetDeviceIds.Contains(deviceId) && _acknowledgedDeviceIds.Add(deviceId); } }
        public bool TryRecordResult(string deviceId) { lock (_sync) { if (!_targetDeviceIds.Contains(deviceId)) return false; _acknowledgedDeviceIds.Add(deviceId); return _completedDeviceIds.Add(deviceId); } }
        public CommandSummary CreateSummary(bool timedOut)
        {
            lock (_sync)
            {
                var timedOutDeviceIds = timedOut ? _targetDeviceIds.Except(_completedDeviceIds, StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<string>();
                return new CommandSummary { CommandId = CommandId, CommandName = CommandName, TargetDeviceIds = _targetDeviceIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(), AcknowledgedDeviceIds = _acknowledgedDeviceIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(), CompletedDeviceIds = _completedDeviceIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(), TimedOutDeviceIds = timedOutDeviceIds, TimeoutMs = TimeoutMs, TimedOut = timedOut };
            }
        }
        public void Dispose() => Cancellation.Dispose();
    }
    private sealed class ServerClientConnection : IDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public ServerClientConnection(TcpClient client) { Client = client; ConnectionId = Guid.NewGuid().ToString("N"); }
        public string ConnectionId { get; }
        public string DeviceId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
        public DateTimeOffset AuthenticatedUntilUtc { get; set; }
        public DateTimeOffset? LastHeartbeatUtc { get; set; }
        public TcpClient Client { get; }
        public StreamWriter? Writer { get; set; }
        public bool IsConsoleAdmin { get; set; }
        public string DeviceIdOrConnectionId => string.IsNullOrWhiteSpace(DeviceId) ? ConnectionId : DeviceId;
        public async Task SendAsync(SocketEnvelope envelope, CancellationToken cancellationToken)
        {
            envelope.ProtocolVersion = string.IsNullOrWhiteSpace(envelope.ProtocolVersion) ? CommandProtocol.ProtocolVersion : envelope.ProtocolVersion;
            envelope.DeviceId = string.IsNullOrWhiteSpace(envelope.DeviceId) ? DeviceIdOrConnectionId : envelope.DeviceId;
            envelope.ClientId = string.IsNullOrWhiteSpace(envelope.ClientId) ? envelope.DeviceId : envelope.ClientId;
            if (IsConsoleAdmin) { Console.WriteLine(JsonLineSocketProtocol.Serialize(envelope)); return; }
            if (Writer is null) return;
            await _sendLock.WaitAsync(cancellationToken);
            try { await Writer.WriteLineAsync(JsonLineSocketProtocol.Serialize(envelope)); }
            catch (IOException) { } catch (ObjectDisposedException) { }
            finally { _sendLock.Release(); }
        }
        public void Dispose() { try { Client.Close(); Client.Dispose(); } finally { _sendLock.Dispose(); } }
    }
}

public sealed record AuthenticatedDeviceInfo(string DeviceId, string Role, DateTimeOffset? LastHeartbeatUtc, DateTimeOffset AuthenticatedUntilUtc);

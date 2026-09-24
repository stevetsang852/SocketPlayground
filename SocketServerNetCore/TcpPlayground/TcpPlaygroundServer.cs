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

    public TcpPlaygroundServer(TcpPlaygroundServerOptions options)
    {
        _options = options;
    }

    public int Port { get; private set; }

    public IReadOnlyList<AuthenticatedDeviceInfo> GetAuthenticatedDevices()
        => _authenticatedConnections.Values
            .Where(connection => connection.IsAuthenticated)
            .Select(connection => new AuthenticatedDeviceInfo(
                connection.DeviceId,
                connection.Role,
                connection.LastHeartbeatUtc,
                connection.AuthenticatedUntilUtc))
            .OrderBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Task DispatchConsoleCommandAsync(
        string commandName,
        string targetMode = "all",
        IEnumerable<string>? targetDeviceIds = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = SocketEnvelope.Create(
            type: "admin-command",
            deviceId: CommandProtocol.ServerDeviceId,
            payload: new AdminCommandRequest
            {
                CommandId = Guid.NewGuid().ToString("N"),
                CommandName = commandName,
                TargetMode = targetMode,
                TargetDeviceIds = targetDeviceIds?.ToArray(),
                TimeoutMs = timeoutMs
            },
            role: DeviceRoles.Admin);

        return HandleAdminCommandAsync(CreateConsoleAdminConnection(), envelope, cancellationToken);
    }

    private ServerClientConnection CreateConsoleAdminConnection()
    {
        return new ServerClientConnection(new TcpClient())
        {
            DeviceId = CommandProtocol.ServerDeviceId,
            Role = DeviceRoles.Admin,
            IsAuthenticated = true,
            AuthenticatedUntilUtc = DateTimeOffset.MaxValue,
            IsConsoleAdmin = true
        };
    }

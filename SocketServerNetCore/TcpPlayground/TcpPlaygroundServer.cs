using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpPlaygroundServer : IAsyncDisposable
{
    private readonly TcpPlaygroundServerOptions _options;
    private readonly ConcurrentDictionary<string, ServerClientConnection> _connections = new();
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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            throw new InvalidOperationException("The server is already running.");
        }

        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Loopback, _options.Port);
        _listener.Start(_options.Backlog);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptLoopAsync(_lifetimeCts.Token);
        await Task.Yield();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is null)
        {
            return;
        }

        _lifetimeCts?.Cancel();
        _listener.Stop();

        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        if (_acceptLoop is not null)
        {
            await AwaitAndIgnoreSocketShutdownAsync(_acceptLoop);
        }

        Task[] tasks;
        lock (_taskLock)
        {
            tasks = _connectionTasks.ToArray();
        }

        await Task.WhenAll(tasks.Select(AwaitAndIgnoreSocketShutdownAsync));

        _connections.Clear();
        _acceptLoop = null;
        _listener = null;
        _lifetimeCts?.Dispose();
        _lifetimeCts = null;
        Port = 0;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var connection = new ServerClientConnection(client);
            _connections[connection.ConnectionId] = connection;
            var task = HandleClientAsync(connection, cancellationToken);
            lock (_taskLock)
            {
                _connectionTasks.Add(task);
            }

            _ = task.ContinueWith(_ =>
            {
                lock (_taskLock)
                {
                    _connectionTasks.Remove(task);
                }
            }, TaskScheduler.Default);
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
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (!JsonLineSocketProtocol.TryDeserialize(line, out var envelope, out var error))
                {
                    await connection.SendAsync(SocketEnvelope.Create(
                        type: "error",
                        clientId: connection.ClientId,
                        payload: new { message = "Malformed JSON line rejected.", detail = error },
                        requestId: Guid.NewGuid().ToString("N")), cancellationToken);
                    break;
                }

                if (!string.IsNullOrWhiteSpace(envelope!.ClientId))
                {
                    connection.ClientId = envelope.ClientId;
                }

                await HandleMessageAsync(connection, envelope, cancellationToken);
            }
        }
        catch (AuthenticationException)
        {
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _connections.TryRemove(connection.ConnectionId, out _);
            connection.Dispose();
        }
    }

    private async Task<Stream> OpenStreamAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        if (_options.ServerCertificate is null)
        {
            return stream;
        }

        var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = _options.ServerCertificate,
            ClientCertificateRequired = false,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
        }, cancellationToken);
        return sslStream;
    }

    private async Task HandleMessageAsync(ServerClientConnection connection, SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.Type)
        {
            case "hello":
                await connection.SendAsync(SocketEnvelope.Create(
                    type: "welcome",
                    clientId: connection.ClientId,
                    payload: new { connectionId = connection.ConnectionId, port = Port },
                    requestId: envelope.RequestId), cancellationToken);
                break;
            case "echo":
                await connection.SendAsync(SocketEnvelope.Create(
                    type: "echo.response",
                    clientId: connection.ClientId,
                    payload: envelope.Payload,
                    requestId: envelope.RequestId), cancellationToken);
                break;
            case "broadcast":
                await BroadcastAsync(SocketEnvelope.Create(
                    type: "broadcast.event",
                    clientId: connection.ClientId,
                    payload: new { fromClientId = connection.ClientId, body = envelope.Payload },
                    requestId: envelope.RequestId), cancellationToken);
                break;
            case "delay":
                var delayRequest = envelope.DeserializePayload<DelayRequest>() ?? new DelayRequest();
                await Task.Delay(TimeSpan.FromMilliseconds(delayRequest.DelayMs), cancellationToken);
                await connection.SendAsync(SocketEnvelope.Create(
                    type: "delay.response",
                    clientId: connection.ClientId,
                    payload: new { message = delayRequest.Message, delayMs = delayRequest.DelayMs },
                    requestId: envelope.RequestId), cancellationToken);
                break;
            case "noop":
                break;
            default:
                await connection.SendAsync(SocketEnvelope.Create(
                    type: "error",
                    clientId: connection.ClientId,
                    payload: new { message = $"Unsupported message type '{envelope.Type}'." },
                    requestId: envelope.RequestId), cancellationToken);
                break;
        }
    }

    private async Task BroadcastAsync(SocketEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (var connection in _connections.Values.ToArray())
        {
            if (connection.Writer is null)
            {
                continue;
            }

            await connection.SendAsync(envelope, cancellationToken);
        }
    }

    private static async Task AwaitAndIgnoreSocketShutdownAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
        catch (AuthenticationException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private sealed class DelayRequest
    {
        public int DelayMs { get; set; }
        public string? Message { get; set; }
    }

    private sealed class ServerClientConnection : IDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public ServerClientConnection(TcpClient client)
        {
            Client = client;
            ConnectionId = Guid.NewGuid().ToString("N");
            ClientId = ConnectionId;
        }

        public string ConnectionId { get; }
        public string ClientId { get; set; }
        public TcpClient Client { get; }
        public StreamWriter? Writer { get; set; }

        public async Task SendAsync(SocketEnvelope envelope, CancellationToken cancellationToken)
        {
            if (Writer is null)
            {
                return;
            }

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await Writer.WriteLineAsync(JsonLineSocketProtocol.Serialize(envelope));
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose()
        {
            try
            {
                Client.Close();
                Client.Dispose();
            }
            finally
            {
                _sendLock.Dispose();
            }
        }
    }
}

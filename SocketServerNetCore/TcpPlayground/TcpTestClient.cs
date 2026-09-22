using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;

namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpTestClient : IAsyncDisposable
{
    private readonly TcpTestClientOptions _options;
    private readonly Channel<SocketEnvelope> _incoming = Channel.CreateUnbounded<SocketEnvelope>();
    private readonly List<SocketEnvelope> _backlog = new();
    private readonly object _backlogLock = new();
    private readonly object _transcriptLock = new();
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;

    public TcpTestClient(TcpTestClientOptions options)
    {
        _options = options;
    }

    public string ClientId => _options.ClientId;
    public List<TcpTranscriptEvent> Transcript { get; } = new();

    public IReadOnlyList<TcpTranscriptEvent> SnapshotTranscript()
    {
        lock (_transcriptLock)
        {
            return Transcript.ToList();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async token =>
        {
            _client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_options.ConnectTimeout);
            await _client.ConnectAsync(_options.Host, _options.Port, timeoutCts.Token);
            var stream = _client.GetStream();
            _reader = JsonLineSocketProtocol.CreateReader(stream);
            _writer = JsonLineSocketProtocol.CreateWriter(stream);
            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _receiveLoop = ReceiveLoopAsync(_receiveCts.Token);
            AddTranscript("client", "connect", $"Connected to {_options.Host}:{_options.Port}");

            var welcome = await SendRequestAsync("hello", new { name = ClientId }, envelope => envelope.Type == "welcome", token);
            AddTranscript("server", welcome.Type, "Handshake complete");
        }, cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        if (_receiveCts is not null)
        {
            _receiveCts.Cancel();
        }

        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop;
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
        }

        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
        _receiveCts?.Dispose();
        _receiveCts = null;
        _receiveLoop = null;
        _reader = null;
        _writer = null;
        _client = null;
        AddTranscript("client", "disconnect", "Disconnected");
    }

    public async Task<SocketEnvelope> SendRequestAsync(
        string type,
        object? payload,
        Func<SocketEnvelope, bool> responseMatcher,
        CancellationToken cancellationToken = default,
        TimeSpan? timeoutOverride = null)
    {
        var envelope = SocketEnvelope.Create(type, ClientId, payload);
        await SendAsync(envelope, cancellationToken);
        return await WaitForMessageAsync(
            incoming => incoming.RequestId == envelope.RequestId && responseMatcher(incoming),
            timeoutOverride,
            cancellationToken);
    }

    public async Task SendAsync(SocketEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        await _writer.WriteLineAsync(JsonLineSocketProtocol.Serialize(envelope));
        AddTranscript("client", envelope.Type, JsonLineSocketProtocol.Serialize(envelope));
    }

    public async Task<SocketEnvelope> WaitForMessageAsync(
        Func<SocketEnvelope, bool> matcher,
        TimeSpan? timeoutOverride = null,
        CancellationToken cancellationToken = default)
    {
        var timeout = timeoutOverride ?? _options.ResponseTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        lock (_backlogLock)
        {
            var match = _backlog.FirstOrDefault(matcher);
            if (match is not null)
            {
                _backlog.Remove(match);
                return match;
            }
        }

        while (true)
        {
            try
            {
                var envelope = await _incoming.Reader.ReadAsync(timeoutCts.Token);
                if (matcher(envelope))
                {
                    return envelope;
                }

                lock (_backlogLock)
                {
                    _backlog.Add(envelope);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                AddTranscript("client", "timeout", $"Timed out waiting {timeout.TotalMilliseconds}ms for message.");
                throw new TimeoutException($"Timed out waiting {timeout.TotalMilliseconds}ms for a matching message.");
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (!JsonLineSocketProtocol.TryDeserialize(line, out var envelope, out _))
            {
                AddTranscript("server", "malformed", line);
                continue;
            }

            AddTranscript("server", envelope!.Type, line);
            await _incoming.Writer.WriteAsync(envelope, cancellationToken);
        }
    }

    private async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var attempts = 0;
        Exception? lastException = null;
        do
        {
            attempts++;
            try
            {
                await action(cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is SocketException or TimeoutException or IOException)
            {
                lastException = exception;
                AddTranscript("client", "retry", $"Attempt {attempts} failed: {exception.Message}");
                if (attempts > _options.RetryCount)
                {
                    break;
                }

                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }
        while (attempts <= _options.RetryCount);

        throw lastException ?? new InvalidOperationException("The operation failed without an exception.");
    }

    private void AddTranscript(string direction, string messageType, string detail)
    {
        lock (_transcriptLock)
        {
            Transcript.Add(new TcpTranscriptEvent
            {
                Direction = direction,
                MessageType = messageType,
                Detail = detail,
                TimestampUtc = DateTimeOffset.UtcNow
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

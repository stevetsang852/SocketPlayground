using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SocketServerNetCore.TcpPlayground;

namespace SocketServerNetCore.Tests;

[TestClass]
public sealed class TlsTcpPlaygroundIntegrationTests
{
    private const string AuthSecret = "integration-test-secret";

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task UnauthenticatedConnectionMustAuthenticateFirstAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);

        var response = await SendTlsMessageAsync(server.Port, certificate, """{"type":"heartbeat","deviceId":"intruder"}""");
        StringAssert.Contains(response, "\"type\":\"error\"");
        StringAssert.Contains(response, "\"code\":\"auth_required\"");

        await using var trustedClient = CreateTrustedClient(server.Port, certificate, "trusted-client");
        await trustedClient.ConnectAsync();
        var heartbeat = await trustedClient.SendRequestAsync("heartbeat", new { sequence = 1 }, envelope => envelope.Type == "heartbeat.ack");
        Assert.AreEqual("heartbeat.ack", heartbeat.Type);
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task InvalidTokenIsRejectedAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var client = new TcpTestClient(new TcpTestClientOptions
        {
            ClientId = "invalid-token-client",
            Role = DeviceRoles.Client,
            Port = server.Port,
            RetryCount = 0,
            UseTls = true,
            AccessToken = "not-a-valid-token",
            RemoteCertificateValidationCallback = MatchCertificate(certificate)
        });

        await Assert.ThrowsExceptionAsync<AuthenticationException>(() => client.ConnectAsync());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task DuplicateDeviceIsRejectedByDefaultAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var original = CreateTrustedClient(server.Port, certificate, "shared-device");
        await using var duplicate = CreateTrustedClient(server.Port, certificate, "shared-device");

        await original.ConnectAsync();
        await Assert.ThrowsExceptionAsync<AuthenticationException>(() => duplicate.ConnectAsync());

        var heartbeat = await original.SendRequestAsync("heartbeat", new { sequence = 2 }, envelope => envelope.Type == "heartbeat.ack");
        Assert.AreEqual("heartbeat.ack", heartbeat.Type);
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task NonAdminCannotSubmitAdminCommandsAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var client = CreateTrustedClient(server.Port, certificate, "plain-client");

        await client.ConnectAsync();
        var error = await client.SendRequestAsync("admin-command", new
        {
            commandId = "deny-me",
            commandName = "health-check",
            targetMode = "all"
        }, envelope => envelope.Type == "error");

        Assert.AreEqual("forbidden", error.Payload?.GetProperty("code").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task AllowlistedAdminCommandIsBroadcastAndAggregatedAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var admin = CreateTrustedClient(server.Port, certificate, "admin-1", DeviceRoles.Admin);
        await using var alpha = CreateTrustedClient(server.Port, certificate, "alpha");
        await using var bravo = CreateTrustedClient(server.Port, certificate, "bravo");

        await Task.WhenAll(admin.ConnectAsync(), alpha.ConnectAsync(), bravo.ConnectAsync());

        var accepted = await admin.SendRequestAsync("admin-command", new
        {
            commandId = "health-broadcast",
            commandName = "health-check",
            targetMode = "all",
            timeoutMs = 1000
        }, envelope => envelope.Type == "admin-command.accepted");

        var commandId = accepted.Payload?.GetProperty("commandId").GetString();
        Assert.AreEqual("health-broadcast", commandId);

        var alphaCommand = await alpha.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId);
        var bravoCommand = await bravo.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId);
        Assert.AreEqual("health-check", alphaCommand.Payload?.GetProperty("commandName").GetString());
        Assert.AreEqual("health-check", bravoCommand.Payload?.GetProperty("commandName").GetString());

        await SendCommandAckAsync(alpha, commandId!);
        await SendCommandAckAsync(bravo, commandId!);
        await SendCommandResultAsync(alpha, commandId!, "health-check", new { status = "ok", observedBy = "alpha" });
        await SendCommandResultAsync(bravo, commandId!, "health-check", new { status = "ok", observedBy = "bravo" });

        var acknowledgements = new[]
        {
            await admin.WaitForMessageAsync(envelope => envelope.Type == "command-ack" && envelope.CorrelationId == commandId),
            await admin.WaitForMessageAsync(envelope => envelope.Type == "command-ack" && envelope.CorrelationId == commandId)
        };
        CollectionAssert.AreEquivalent(new[] { "alpha", "bravo" }, acknowledgements.Select(envelope => envelope.DeviceId).ToArray());

        var results = new[]
        {
            await admin.WaitForMessageAsync(envelope => envelope.Type == "command-result" && envelope.CorrelationId == commandId),
            await admin.WaitForMessageAsync(envelope => envelope.Type == "command-result" && envelope.CorrelationId == commandId)
        };
        CollectionAssert.AreEquivalent(new[] { "alpha", "bravo" }, results.Select(envelope => envelope.DeviceId).ToArray());

        var summary = await admin.WaitForMessageAsync(envelope => envelope.Type == "command-summary" && envelope.CorrelationId == commandId);
        CollectionAssert.AreEquivalent(new[] { "alpha", "bravo" }, summary.Payload?.GetProperty("completedDeviceIds").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.IsFalse(summary.Payload?.GetProperty("timedOut").GetBoolean() ?? true);
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task AllowlistRejectsUnknownCommandAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var admin = CreateTrustedClient(server.Port, certificate, "admin-1", DeviceRoles.Admin);

        await admin.ConnectAsync();
        var error = await admin.SendRequestAsync("admin-command", new
        {
            commandId = "no-shell",
            commandName = "exec-shell",
            targetMode = "all"
        }, envelope => envelope.Type == "error");

        Assert.AreEqual("command_not_allowed", error.Payload?.GetProperty("code").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task MalformedFrameIsIsolatedAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var goodClient = CreateTrustedClient(server.Port, certificate, "good-client");
        await goodClient.ConnectAsync();

        var malformedResponse = await SendTlsMessageAsync(server.Port, certificate, "{oops}");
        StringAssert.Contains(malformedResponse, "\"code\":\"malformed_frame\"");

        var echo = await goodClient.SendRequestAsync("echo", new { message = "healthy" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("healthy", echo.Payload?.GetProperty("message").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task CommandTimeoutProducesSummaryAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate, defaultCommandTimeout: TimeSpan.FromMilliseconds(250));
        await using var admin = CreateTrustedClient(server.Port, certificate, "admin-1", DeviceRoles.Admin);
        await using var slowClient = CreateTrustedClient(server.Port, certificate, "slow-client");

        await Task.WhenAll(admin.ConnectAsync(), slowClient.ConnectAsync());

        var accepted = await admin.SendRequestAsync("admin-command", new
        {
            commandId = "slow-diagnostics",
            commandName = "collect-diagnostics",
            targetMode = "devices",
            targetDeviceIds = new[] { "slow-client" },
            timeoutMs = 250
        }, envelope => envelope.Type == "admin-command.accepted");

        var commandId = accepted.Payload?.GetProperty("commandId").GetString();
        var inbound = await slowClient.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId);
        Assert.AreEqual("collect-diagnostics", inbound.Payload?.GetProperty("commandName").GetString());

        var summary = await admin.WaitForMessageAsync(
            envelope => envelope.Type == "command-summary" && envelope.CorrelationId == commandId,
            timeoutOverride: TimeSpan.FromSeconds(2));
        Assert.IsTrue(summary.Payload?.GetProperty("timedOut").GetBoolean() ?? false);
        CollectionAssert.AreEqual(new[] { "slow-client" }, summary.Payload?.GetProperty("timedOutDeviceIds").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task ReconnectAndClientDeduplicatesCommandIdAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var admin = CreateTrustedClient(server.Port, certificate, "admin-1", DeviceRoles.Admin);
        var dedup = new DeduplicatingAgentHarness(server.Port, certificate, "dedup-agent");

        try
        {
            await admin.ConnectAsync();
            await dedup.ConnectAsync();

            var firstResult = await DispatchAndHandleDeduplicatedCommandAsync(admin, dedup, "sticky-command");
            Assert.IsFalse(firstResult.Payload?.GetProperty("duplicate").GetBoolean() ?? true);

            await dedup.DisconnectAsync();
            await dedup.ConnectAsync();

            var secondResult = await DispatchAndHandleDeduplicatedCommandAsync(admin, dedup, "sticky-command");
            Assert.IsTrue(secondResult.Payload?.GetProperty("duplicate").GetBoolean() ?? false);
        }
        finally
        {
            await dedup.DisposeAsync();
        }
    }

    private static async Task<SocketEnvelope> DispatchAndHandleDeduplicatedCommandAsync(TcpTestClient admin, DeduplicatingAgentHarness dedup, string commandId)
    {
        var accepted = await admin.SendRequestAsync("admin-command", new
        {
            commandId,
            commandName = "refresh-config",
            targetMode = "devices",
            targetDeviceIds = new[] { dedup.DeviceId },
            timeoutMs = 1000
        }, envelope => envelope.Type == "admin-command.accepted");

        var effectiveCommandId = accepted.Payload?.GetProperty("commandId").GetString() ?? commandId;
        await dedup.ProcessNextCommandAsync(effectiveCommandId);
        var result = await admin.WaitForMessageAsync(envelope => envelope.Type == "command-result" && envelope.CorrelationId == effectiveCommandId);
        var summary = await admin.WaitForMessageAsync(envelope => envelope.Type == "command-summary" && envelope.CorrelationId == effectiveCommandId);
        Assert.IsFalse(summary.Payload?.GetProperty("timedOut").GetBoolean() ?? true);
        return result;
    }

    private static async Task SendCommandAckAsync(TcpTestClient client, string commandId)
        => await client.SendAsync(SocketEnvelope.Create(
            type: "command-ack",
            deviceId: client.ClientId,
            payload: new
            {
                commandId,
                status = "accepted"
            },
            role: client.Role,
            correlationId: commandId));

    private static async Task SendCommandResultAsync(TcpTestClient client, string commandId, string commandName, object result, bool duplicate = false)
        => await client.SendAsync(SocketEnvelope.Create(
            type: "command-result",
            deviceId: client.ClientId,
            payload: new
            {
                commandId,
                commandName,
                status = "completed",
                success = true,
                duplicate,
                result
            },
            role: client.Role,
            correlationId: commandId));

    private static async Task<TcpPlaygroundServer> StartTlsServerAsync(
        X509Certificate2 certificate,
        DuplicateSessionPolicy duplicateSessionPolicy = DuplicateSessionPolicy.RejectNew,
        TimeSpan? defaultCommandTimeout = null)
    {
        var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret,
            DuplicateSessionPolicy = duplicateSessionPolicy,
            DefaultCommandTimeout = defaultCommandTimeout ?? TimeSpan.FromMilliseconds(500)
        });

        await server.StartAsync();
        return server;
    }

    private static TcpTestClient CreateTrustedClient(int port, X509Certificate2 certificate, string clientId, string role = DeviceRoles.Client)
        => new(new TcpTestClientOptions
        {
            ClientId = clientId,
            Role = role,
            Port = port,
            RetryCount = 0,
            UseTls = true,
            AuthenticationSecret = AuthSecret,
            RemoteCertificateValidationCallback = MatchCertificate(certificate)
        });

    private static Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool> MatchCertificate(X509Certificate2 certificate)
        => (presentedCertificate, _, _) => presentedCertificate?.Thumbprint == certificate.Thumbprint;

    private static RemoteCertificateValidationCallback MatchCertificateCallback(X509Certificate2 certificate)
        => (_, presentedCertificate, chain, sslPolicyErrors)
            => MatchCertificate(certificate)(
                presentedCertificate is null ? null : new X509Certificate2(presentedCertificate),
                chain,
                sslPolicyErrors);

    private static async Task<string> SendTlsMessageAsync(int port, X509Certificate2 certificate, string payload)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);

        using var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, MatchCertificateCallback(certificate));
        await sslStream.AuthenticateAsClientAsync("localhost");

        using var writer = new StreamWriter(sslStream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(sslStream, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(payload);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        return (await reader.ReadLineAsync(timeout.Token)) ?? string.Empty;
    }

    private sealed class DeduplicatingAgentHarness : IAsyncDisposable
    {
        private readonly HashSet<string> _seenCommandIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly TcpTestClient _client;

        public DeduplicatingAgentHarness(int port, X509Certificate2 certificate, string deviceId)
        {
            DeviceId = deviceId;
            _client = CreateTrustedClient(port, certificate, deviceId);
        }

        public string DeviceId { get; }

        public Task ConnectAsync() => _client.ConnectAsync();

        public Task DisconnectAsync() => _client.DisconnectAsync();

        public async Task ProcessNextCommandAsync(string commandId)
        {
            var command = await _client.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId);
            Assert.AreEqual(commandId, command.CorrelationId);

            await SendCommandAckAsync(_client, commandId);
            var duplicate = !_seenCommandIds.Add(commandId);
            await SendCommandResultAsync(_client, commandId, "refresh-config", new
            {
                status = duplicate ? "replayed" : "applied",
                observedBy = DeviceId
            }, duplicate);
        }

        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
        }
    }
}

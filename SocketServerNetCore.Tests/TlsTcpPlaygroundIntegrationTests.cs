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
    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsHandshakeReportsEncryptedTransportAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var client = CreateTrustedClient(server.Port, certificate, "handshake-client");

        await client.ConnectAsync();

        Assert.IsTrue(client.IsTlsAuthenticated);
        Assert.IsTrue(client.IsEncryptedTransport);
        Assert.IsTrue(client.SnapshotTranscript().Any(eventEntry => eventEntry.MessageType == "tls.handshake"));
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsEchoRoundTripAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var client = CreateTrustedClient(server.Port, certificate, "echo-client");

        await client.ConnectAsync();
        var response = await client.SendRequestAsync("echo", new { message = "hello over tls" }, envelope => envelope.Type == "echo.response");

        Assert.AreEqual("hello over tls", response.Payload?.GetProperty("message").GetString());
        Assert.AreEqual(client.ClientId, response.ClientId);
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsBroadcastProvidesBidirectionalMessageFlowAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var alpha = CreateTrustedClient(server.Port, certificate, "alpha");
        await using var bravo = CreateTrustedClient(server.Port, certificate, "bravo");

        await Task.WhenAll(alpha.ConnectAsync(), bravo.ConnectAsync());

        var request = SocketEnvelope.Create("broadcast", alpha.ClientId, new { message = "fanout" });
        await alpha.SendAsync(request);

        var responses = await Task.WhenAll(
            alpha.WaitForMessageAsync(envelope => envelope.Type == "broadcast.event" && envelope.RequestId == request.RequestId),
            bravo.WaitForMessageAsync(envelope => envelope.Type == "broadcast.event" && envelope.RequestId == request.RequestId));

        Assert.AreEqual(2, responses.Length);
        Assert.IsTrue(responses.All(response => response.Payload?.GetProperty("fromClientId").GetString() == "alpha"));
        Assert.IsTrue(responses.All(response => response.Payload?.GetProperty("body").GetProperty("message").GetString() == "fanout"));
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsServerHandlesConcurrentClientsAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        var clients = Enumerable.Range(0, 4)
            .Select(index => CreateTrustedClient(server.Port, certificate, $"client-{index}"))
            .ToArray();

        try
        {
            await Task.WhenAll(clients.Select(client => client.ConnectAsync()));

            var responses = await Task.WhenAll(clients.Select((client, index) =>
                client.SendRequestAsync(
                    "echo",
                    new { message = $"message-{index}" },
                    envelope => envelope.Type == "echo.response")));

            for (var index = 0; index < responses.Length; index++)
            {
                Assert.AreEqual($"message-{index}", responses[index].Payload?.GetProperty("message").GetString());
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsClientRejectsUntrustedCertificateWithoutOptInAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var untrustedClient = new TcpTestClient(new TcpTestClientOptions
        {
            ClientId = "untrusted-client",
            Port = server.Port,
            RetryCount = 0,
            UseTls = true
        });

        var exception = await Assert.ThrowsExceptionAsync<AuthenticationException>(() => untrustedClient.ConnectAsync());
        Assert.IsTrue(
            exception.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase),
            $"Unexpected TLS failure message: {exception.Message}");

        await using var trustedClient = CreateTrustedClient(server.Port, certificate, "trusted-client");
        await trustedClient.ConnectAsync();
        var response = await trustedClient.SendRequestAsync("echo", new { message = "still running" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("still running", response.Payload?.GetProperty("message").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsServerReturnsStructuredErrorForMalformedMessagesAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var goodClient = CreateTrustedClient(server.Port, certificate, "good-client");
        await goodClient.ConnectAsync();

        var malformedResponse = await SendMalformedTlsMessageAsync(server.Port, certificate, "{oops}");
        StringAssert.Contains(malformedResponse, "\"type\":\"error\"");
        StringAssert.Contains(malformedResponse, "Malformed JSON line rejected.");

        var echo = await goodClient.SendRequestAsync("echo", new { message = "healthy" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("healthy", echo.Payload?.GetProperty("message").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsClientCanReconnectAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var client = CreateTrustedClient(server.Port, certificate, "reconnect-client");

        await client.ConnectAsync();
        await client.DisconnectAsync();
        await client.ConnectAsync();

        var response = await client.SendRequestAsync("echo", new { message = "after reconnect" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("after reconnect", response.Payload?.GetProperty("message").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public async Task TlsClientHonorsTimeoutsAndCancellationAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartTlsServerAsync(certificate);
        await using var client = new TcpTestClient(new TcpTestClientOptions
        {
            ClientId = "timing-client",
            Port = server.Port,
            RetryCount = 0,
            UseTls = true,
            ResponseTimeout = TimeSpan.FromMilliseconds(200),
            RemoteCertificateValidationCallback = MatchCertificate(certificate)
        });

        await client.ConnectAsync();

        await Assert.ThrowsExceptionAsync<TimeoutException>(() => client.SendRequestAsync(
            "delay",
            new { delayMs = 1000, message = "slow" },
            envelope => envelope.Type == "delay.response",
            timeoutOverride: TimeSpan.FromMilliseconds(150)));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try
        {
            await client.SendRequestAsync(
                "delay",
                new { delayMs = 1000, message = "cancelled" },
                envelope => envelope.Type == "delay.response",
                cancellation.Token,
                timeoutOverride: TimeSpan.FromSeconds(2));
            Assert.Fail("Expected the delayed TLS request to be cancelled.");
        }
        catch (OperationCanceledException)
        {
        }

        await using var recoveryClient = CreateTrustedClient(server.Port, certificate, "recovery-client");
        await recoveryClient.ConnectAsync();
        var recoveryResponse = await recoveryClient.SendRequestAsync("echo", new { message = "recovered" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("recovered", recoveryResponse.Payload?.GetProperty("message").GetString());
    }

    private static async Task<TcpPlaygroundServer> StartTlsServerAsync(X509Certificate2 certificate)
    {
        var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate
        });

        await server.StartAsync();
        return server;
    }

    private static TcpTestClient CreateTrustedClient(int port, X509Certificate2 certificate, string clientId)
        => new(new TcpTestClientOptions
        {
            ClientId = clientId,
            Port = port,
            RetryCount = 0,
            UseTls = true,
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

    private static async Task<string> SendMalformedTlsMessageAsync(int port, X509Certificate2 certificate, string payload)
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
}

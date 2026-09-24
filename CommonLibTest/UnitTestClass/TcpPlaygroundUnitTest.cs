using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SocketServerNetCore.TcpPlayground;

namespace CommonLibTest;

[TestClass]
public class TcpPlaygroundUnitTest
{
    private const string AuthSecret = "common-lib-test-secret";

    [TestMethod]
    [TestCategory("TcpPlayground")]
    public async Task TcpServerHandlesEchoAndAdminCommandAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret
        });
        await server.StartAsync();

        await using var alpha = CreateClient(server.Port, certificate, "alpha");
        await using var bravo = CreateClient(server.Port, certificate, "bravo");

        await Task.WhenAll(alpha.ConnectAsync(), bravo.ConnectAsync());

        var echo = await alpha.SendRequestAsync("echo", new { message = "test" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("test", echo.Payload?.GetProperty("message").GetString());

        await using var admin = CreateClient(server.Port, certificate, "admin", DeviceRoles.Admin);
        await admin.ConnectAsync();
        var accepted = await admin.SendRequestAsync("admin-command", new
        {
            commandId = "commonlib-health-check",
            commandName = "health-check",
            targetMode = "all",
            timeoutMs = 1000
        }, envelope => envelope.Type == "admin-command.accepted");

        var commandId = accepted.Payload?.GetProperty("commandId").GetString();
        Assert.AreEqual("health-check", (await alpha.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId)).Payload?.GetProperty("commandName").GetString());
        Assert.AreEqual("health-check", (await bravo.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId)).Payload?.GetProperty("commandName").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    public async Task TcpServerIsolatesMalformedClientAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret
        });
        await server.StartAsync();
        await using var goodClient = CreateClient(server.Port, certificate, "good");
        await goodClient.ConnectAsync();

        using var badClient = new TcpClient();
        await badClient.ConnectAsync("127.0.0.1", server.Port);
        using var sslStream = new SslStream(badClient.GetStream(), leaveInnerStreamOpen: false, (_, presentedCertificate, _, _) => { if (presentedCertificate is null) return false; using var presented = new X509Certificate2(presentedCertificate.GetRawCertData()); return string.Equals(presented.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase); });
        await sslStream.AuthenticateAsClientAsync("localhost");
        await using (var writer = new StreamWriter(sslStream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" })
        {
            await writer.WriteLineAsync("{oops}");
        }

        var echo = await goodClient.SendRequestAsync("echo", new { message = "healthy" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("healthy", echo.Payload?.GetProperty("message").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    public async Task ScenarioRunnerProducesPassingJsonReportAsync()
    {
        var reportPath = Path.Combine(Path.GetTempPath(), $"socket-playground-{Guid.NewGuid():N}.json");
        var runner = new SocketScenarioRunner();

        var report = await runner.RunAsync(new SocketScenarioRunnerOptions
        {
            ReportPath = reportPath,
            ResponseTimeout = TimeSpan.FromSeconds(2),
            RetryCount = 0,
            AuthenticationSecret = AuthSecret,
            AllowUntrustedCertificates = true
        });

        Assert.IsTrue(report.AllPassed);
        Assert.IsTrue(File.Exists(report.ReportPath));
        StringAssert.Contains(await File.ReadAllTextAsync(report.ReportPath), "authenticated heartbeat");
    }

    private static TcpTestClient CreateClient(int port, X509Certificate2 certificate, string clientId, string role = DeviceRoles.Client)
        => new(new TcpTestClientOptions
        {
            ClientId = clientId,
            Role = role,
            Port = port,
            RetryCount = 0,
            ResponseTimeout = TimeSpan.FromSeconds(1),
            UseTls = true,
            AuthenticationSecret = AuthSecret,
            RemoteCertificateValidationCallback = (presentedCertificate, _, _) => presentedCertificate is not null && string.Equals(presentedCertificate.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase)
        });
}

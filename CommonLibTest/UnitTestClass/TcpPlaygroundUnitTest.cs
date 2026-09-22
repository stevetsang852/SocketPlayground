using System.Net.Sockets;
using System.Text;
using SocketServerNetCore.TcpPlayground;

namespace CommonLibTest;

[TestClass]
public class TcpPlaygroundUnitTest
{
    [TestMethod]
    [TestCategory("TcpPlayground")]
    public async Task TcpServerHandlesEchoAndBroadcastAsync()
    {
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions());
        await server.StartAsync();

        await using var alpha = CreateClient(server.Port, "alpha");
        await using var bravo = CreateClient(server.Port, "bravo");

        await Task.WhenAll(alpha.ConnectAsync(), bravo.ConnectAsync());

        var echo = await alpha.SendRequestAsync("echo", new { message = "test" }, envelope => envelope.Type == "echo.response");
        Assert.AreEqual("test", echo.Payload?.GetProperty("message").GetString());

        var request = SocketEnvelope.Create("broadcast", alpha.ClientId, new { message = "fanout" });
        await alpha.SendAsync(request);
        var responses = await Task.WhenAll(
            alpha.WaitForMessageAsync(envelope => envelope.Type == "broadcast.event" && envelope.RequestId == request.RequestId),
            bravo.WaitForMessageAsync(envelope => envelope.Type == "broadcast.event" && envelope.RequestId == request.RequestId));

        Assert.AreEqual(2, responses.Length);
        Assert.IsTrue(responses.All(response => response.Payload?.GetProperty("fromClientId").GetString() == "alpha"));
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    public async Task TcpServerIsolatesMalformedClientAsync()
    {
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions());
        await server.StartAsync();
        await using var goodClient = CreateClient(server.Port, "good");
        await goodClient.ConnectAsync();

        using var badClient = new TcpClient();
        await badClient.ConnectAsync("127.0.0.1", server.Port);
        await using (var writer = new StreamWriter(badClient.GetStream(), new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" })
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
            ResponseTimeout = TimeSpan.FromMilliseconds(500),
            RetryCount = 0
        });

        Assert.IsTrue(report.AllPassed);
        Assert.IsTrue(File.Exists(report.ReportPath));
        StringAssert.Contains(await File.ReadAllTextAsync(report.ReportPath), "echo/round-trip validation");
    }

    private static TcpTestClient CreateClient(int port, string clientId)
        => new(new TcpTestClientOptions
        {
            ClientId = clientId,
            Port = port,
            RetryCount = 0,
            ResponseTimeout = TimeSpan.FromSeconds(1)
        });
}

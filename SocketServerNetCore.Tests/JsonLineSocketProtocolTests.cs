using System.Net;
using System.Net.Sockets;

using SocketServerNetCore.TcpPlayground;

namespace SocketServerNetCore.Tests;

[TestClass]
public sealed class JsonLineSocketProtocolTests
{
    [TestMethod]
    public void TryDeserializePopulatesMissingMetadata()
    {
        const string line = """{"clientId":"alpha","type":"echo","payload":{"message":"hello"}}""";

        var success = JsonLineSocketProtocol.TryDeserialize(line, out var envelope, out var error);

        Assert.IsTrue(success, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual("alpha", envelope.ClientId);
        Assert.AreEqual("echo", envelope.Type);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.RequestId));
        Assert.AreNotEqual(default, envelope.TimestampUtc);
        Assert.AreEqual("hello", envelope.Payload?.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task CreateWriterUsesUtf8LfFramingAsync()
    {
        var envelope = SocketEnvelope.Create("echo", "alpha", new { message = "line framed" });
        var serialized = JsonLineSocketProtocol.Serialize(envelope);

        await using var stream = new MemoryStream();
        await using (var writer = JsonLineSocketProtocol.CreateWriter(stream))
        {
            await writer.WriteLineAsync(serialized);
            await writer.FlushAsync();
        }

        var buffer = stream.ToArray();
        CollectionAssert.AreNotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, buffer.Take(3).ToArray());
        Assert.AreEqual((byte)'\n', buffer[^1]);
        CollectionAssert.DoesNotContain(buffer, (byte)'\r');

        stream.Position = 0;
        using var reader = JsonLineSocketProtocol.CreateReader(stream);
        var line = await reader.ReadLineAsync();
        Assert.AreEqual(serialized, line);
    }

    [TestMethod]
    public void TryDeserializeRejectsMalformedJson()
    {
        var success = JsonLineSocketProtocol.TryDeserialize("{oops}", out var envelope, out var error);

        Assert.IsFalse(success);
        Assert.IsNull(envelope);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }
}

[TestClass]
public sealed class TcpTestClientRetryTests
{
    [TestMethod]
    public async Task ConnectAsyncRetriesUntilServerStartsAsync()
    {
        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();

        await using var client = new TcpTestClient(new TcpTestClientOptions
        {
            ClientId = "retry-client",
            Port = port,
            ConnectTimeout = TimeSpan.FromMilliseconds(150),
            ResponseTimeout = TimeSpan.FromSeconds(1),
            RetryCount = 3,
            RetryDelay = TimeSpan.FromMilliseconds(150)
        });

        var connectTask = client.ConnectAsync();
        await Task.Delay(100);

        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            Port = port
        });
        await server.StartAsync();
        await connectTask;

        Assert.IsTrue(client.SnapshotTranscript().Any(entry => entry.MessageType == "retry"));

        var response = await client.SendRequestAsync(
            "echo",
            new { message = "after retry" },
            envelope => envelope.Type == "echo.response");

        Assert.AreEqual("after retry", response.Payload?.GetProperty("message").GetString());
    }
}

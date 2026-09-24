using System.Text;
using System.Security.Cryptography.X509Certificates;
using CommonClassLibrary;
using System.Text.Json;
using Payload;
using SocketServerNetCore.TcpPlayground;

namespace CommonLibTest;

[TestClass]
public sealed class CanonicalLegacyBridgeTests
{
    private const string AuthSecret = "canonical-legacy-bridge-secret";

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void AllowlistExcludesLegacyWhenFlagOffAndIncludesWhenOn()
    {
        var off = CommandProtocol.GetAllowedCommands(allowLegacyCommands: false);
        var on = CommandProtocol.GetAllowedCommands(allowLegacyCommands: true);

        foreach (var command in CommandProtocol.SafeCommands)
        {
            Assert.IsTrue(off.Contains(command), $"safe missing when off: {command}");
            Assert.IsTrue(on.Contains(command), $"safe missing when on: {command}");
        }

        foreach (var command in CommandProtocol.LegacyCommands)
        {
            Assert.IsFalse(off.Contains(command), $"legacy present when off: {command}");
            Assert.IsTrue(on.Contains(command), $"legacy missing when on: {command}");
        }

        CollectionAssert.AreEquivalent(
            CommandProtocol.LegacyCommands.ToList(),
            LegacyCommandBridge.LegacyCommandNames.ToList());
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    [TestCategory("Tls")]
    public async Task LoginAllowlistOmitsLegacyWhenServerFlagOffAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret,
            AllowLegacyCommands = false
        });
        await server.StartAsync();

        using var agent = new CanonicalTcpClient("127.0.0.1", server.Port, "dotnet-agent-1", "client", allowUntrusted: true);
        var authenticated = await agent.ConnectAndLoginAsync("dotnet-agent-1", AuthSecret);
        var allowlist = authenticated.GetProperty("payload").GetProperty("commandAllowlist")
            .EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var legacy in CommandProtocol.LegacyCommands)
        {
            Assert.IsFalse(allowlist.Contains(legacy), legacy);
            Assert.IsFalse(agent.AllowedCommandNames.Contains(legacy), legacy);
        }

        Assert.IsTrue(agent.AllowedCommandNames.Contains("health-check"));
        Assert.AreEqual("rejected", JsonSerializer.SerializeToElement(agent.Execute("upload")).GetProperty("status").GetString());
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    [TestCategory("Tls")]
    public async Task DispatchLegacyRejectedWhenServerFlagOffAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret,
            AllowLegacyCommands = false,
            DefaultCommandTimeout = TimeSpan.FromSeconds(2)
        });
        await server.StartAsync();

        using var agent = new CanonicalTcpClient("127.0.0.1", server.Port, "client-a", "client", allowUntrusted: true);
        await agent.ConnectAndLoginAsync("client-a", AuthSecret);

        await using var admin = new TcpTestClient(new TcpTestClientOptions
        {
            ClientId = "admin-a",
            Role = DeviceRoles.Admin,
            Port = server.Port,
            RetryCount = 0,
            UseTls = true,
            AuthenticationSecret = AuthSecret,
            RemoteCertificateValidationCallback = (presented, _, _) =>
                presented is not null
                && string.Equals(presented.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase)
        });
        await admin.ConnectAsync();

        var error = await admin.SendRequestAsync("admin-command", new
        {
            commandId = "deny-upload-1",
            commandName = "upload",
            targetMode = "all",
            timeoutMs = 1000,
            arguments = new { data = new { name = "x.txt", action = "upload", path = "/tmp", fileBase64 = "YQ==" } }
        }, envelope => envelope.Type == "error");

        Assert.AreEqual("error", error.Type);
        Assert.AreEqual("command_not_allowed", error.Payload?.GetProperty("code").GetString());
        StringAssert.Contains(error.Payload?.GetProperty("detail").GetString() ?? string.Empty, "Legacy high-risk");
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    [TestCategory("Tls")]
    public async Task CanonicalAgentReceivesLegacyUploadWhenFlagOnAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret,
            AllowLegacyCommands = true,
            DefaultCommandTimeout = TimeSpan.FromSeconds(5)
        });
        await server.StartAsync();

        using var agent = new CanonicalTcpClient("127.0.0.1", server.Port, "dotnet-agent-1", "client", allowUntrusted: true);
        var authenticated = await agent.ConnectAndLoginAsync("dotnet-agent-1", AuthSecret);
        Assert.AreEqual("authenticated", authenticated.GetProperty("type").GetString());
        Assert.IsTrue(agent.AllowedCommandNames.Contains("upload"));

        var loopCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var loop = agent.ProcessCommandsAsync(loopCts.Token);

        var tempDir = Path.Combine(Path.GetTempPath(), "socket-playground-tls-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var arguments = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = "tls-note.txt",
                    action = "upload",
                    path = tempDir,
                    fileBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("tls-upload-ok"))
                }
            });

            await server.DispatchConsoleCommandAsync(
                "upload",
                targetMode: "devices",
                targetDeviceIds: new[] { "dotnet-agent-1" },
                timeoutMs: 4000,
                arguments: arguments);

            for (var i = 0; i < 40 && !File.Exists(Path.Combine(tempDir, "tls-note.txt")); i++)
            {
                await Task.Delay(50);
            }

            Assert.AreEqual("tls-upload-ok", File.ReadAllText(Path.Combine(tempDir, "tls-note.txt")));
        }
        finally
        {
            loopCts.Cancel();
            try { await loop; } catch (OperationCanceledException) { } catch (IOException) { }
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void UploadHandlerSavesBase64FileWithoutExecutingPatch()
    {
        var bridge = new LegacyCommandBridge();
        var tempDir = Path.Combine(Path.GetTempPath(), "socket-playground-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var bytes = Encoding.UTF8.GetBytes("hello-canonical-bridge");
            var args = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = "note.txt",
                    action = "upload",
                    path = tempDir,
                    fileBase64 = Convert.ToBase64String(bytes)
                }
            });

            var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
            Assert.AreEqual("saved", result.GetProperty("status").GetString());
            Assert.AreEqual(false, result.GetProperty("patched").GetBoolean());
            Assert.AreEqual("hello-canonical-bridge", File.ReadAllText(Path.Combine(tempDir, "note.txt")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void WallpaperRnameIsSafeNoOpWhenDirectoryEmpty()
    {
        var bridge = new LegacyCommandBridge();
        var wallpaperDir = Config.Instance.WallpaperEngineCommandImageDir;
        Directory.CreateDirectory(wallpaperDir);
        var result = JsonSerializer.SerializeToElement(bridge.HandleWallpaperTaskPack(
            JsonSerializer.SerializeToElement(new { data = "rname" })));
        Assert.AreEqual("executed", result.GetProperty("status").GetString());
        Assert.AreEqual("rname", result.GetProperty("data").GetString());
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void HealthCheckStillWorksOnBridgedClient()
    {
        using var client = new CanonicalTcpClient("127.0.0.1", 1, "dotnet-agent-1", "client");
        var json = JsonSerializer.SerializeToElement(client.Execute("health-check"));
        Assert.AreEqual("ok", json.GetProperty("status").GetString());
        Assert.AreEqual("rejected", JsonSerializer.SerializeToElement(client.Execute("not-real")).GetProperty("status").GetString());
        Assert.AreEqual("rejected", JsonSerializer.SerializeToElement(client.Execute("csharp")).GetProperty("status").GetString());
    }
}

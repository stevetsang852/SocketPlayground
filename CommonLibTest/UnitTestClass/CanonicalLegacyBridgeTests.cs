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

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void UploadHandlerRejectsMissingFileContent()
    {
        var bridge = new LegacyCommandBridge();
        var tempDir = Path.Combine(Path.GetTempPath(), "socket-playground-upload-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var args = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = "missing.txt",
                    action = "upload",
                    path = tempDir
                    // no file / fileBase64
                }
            });

            var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
            Assert.AreEqual("rejected", result.GetProperty("status").GetString());
            Assert.AreEqual("no file content provided", result.GetProperty("reason").GetString());
            Assert.IsFalse(File.Exists(Path.Combine(tempDir, "missing.txt")));
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
    public void UploadHandlerAcceptsExplicitEmptyContentAsZeroByteFile()
    {
        var bridge = new LegacyCommandBridge();
        var tempDir = Path.Combine(Path.GetTempPath(), "socket-playground-upload-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var args = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = "empty.txt",
                    action = "upload",
                    path = tempDir,
                    fileBase64 = ""
                }
            });

            var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
            Assert.AreEqual("saved", result.GetProperty("status").GetString());
            var dest = Path.Combine(tempDir, "empty.txt");
            Assert.IsTrue(File.Exists(dest));
            Assert.AreEqual(0, new FileInfo(dest).Length);
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
    public void UploadHandlerRejectsInvalidBase64()
    {
        var bridge = new LegacyCommandBridge();
        var args = JsonSerializer.SerializeToElement(new
        {
            data = new
            {
                name = "bad.txt",
                action = "upload",
                path = Path.GetTempPath(),
                fileBase64 = "!!!not-base64!!!"
            }
        });

        var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
        Assert.AreEqual("rejected", result.GetProperty("status").GetString());
        StringAssert.Contains(result.GetProperty("reason").GetString(), "invalid fileBase64");
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void UploadHandlePathUsesUniqueMillisecondDirs()
    {
        var bridge = new LegacyCommandBridge();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 5; i++)
        {
            var args = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = $"notes-{i}.txt",
                    action = "upgrade",
                    path = "/ignored",
                    fileBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"v{i}"))
                }
            });
            var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
            Assert.AreEqual("saved", result.GetProperty("status").GetString());
            var path = result.GetProperty("path").GetString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(path));
            Assert.IsTrue(path!.Contains("_patch", StringComparison.Ordinal));
            Assert.IsTrue(paths.Add(path!), $"collision on path {path}");
            // Cleanup per-request dir
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void UploadZipWithZeroExesDoesNotAttemptLaunch()
    {
        var bridge = new LegacyCommandBridge();
        var zipBytes = CreateZipWithEntries(("only.txt", Encoding.UTF8.GetBytes("no-exe")));
        var args = JsonSerializer.SerializeToElement(new
        {
            data = new
            {
                name = "zero.exe.zip",
                action = "upgrade",
                path = "/ignored",
                fileBase64 = Convert.ToBase64String(zipBytes)
            }
        });

        var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
        Assert.AreEqual("saved", result.GetProperty("status").GetString());
        Assert.AreEqual(true, result.GetProperty("patched").GetBoolean());
        Assert.AreEqual(false, result.GetProperty("exeLaunched").GetBoolean());
        StringAssert.Contains(result.GetProperty("message").GetString(), "no .exe");
        var path = result.GetProperty("path").GetString();
        Assert.IsTrue(Directory.Exists(path));
        Assert.IsTrue(File.Exists(Path.Combine(path!, "only.txt")));
        Directory.Delete(path!, recursive: true);
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void FileHelperSaveFileReturnsFalseWhenFileBytesMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "socket-playground-fh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.IsFalse(FileHelper.SaveFile(new UploadProps
            {
                path = tempDir,
                name = "x.bin",
                file = null
            }));
            Assert.IsFalse(File.Exists(Path.Combine(tempDir, "x.bin")));

            Assert.IsTrue(FileHelper.SaveFile(new UploadProps
            {
                path = tempDir,
                name = "empty.bin",
                file = Array.Empty<byte>()
            }));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "empty.bin")));
            Assert.AreEqual(0, new FileInfo(Path.Combine(tempDir, "empty.bin")).Length);
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
    public void UploadZipWithMultipleExesSkipsLaunch()
    {
        var bridge = new LegacyCommandBridge();
        var zipBytes = CreateZipWithEntries(
            ("a.exe", Encoding.UTF8.GetBytes("A")),
            ("b.exe", Encoding.UTF8.GetBytes("B")));
        var args = JsonSerializer.SerializeToElement(new
        {
            data = new
            {
                name = "multi.exe.zip",
                action = "upgrade",
                path = "/ignored",
                fileBase64 = Convert.ToBase64String(zipBytes)
            }
        });

        var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
        Assert.AreEqual("saved", result.GetProperty("status").GetString());
        Assert.AreEqual(true, result.GetProperty("patched").GetBoolean());
        Assert.AreEqual(false, result.GetProperty("exeLaunched").GetBoolean());
        StringAssert.Contains(result.GetProperty("message").GetString(), "skipped auto-launch");
        var path = result.GetProperty("path").GetString();
        Assert.IsTrue(Directory.Exists(path));
        Directory.Delete(path!, recursive: true);
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void UploadZipWithSingleExeCleansUpWhenLaunchFails()
    {
        // On non-Windows, StartProcess fails (cmd.exe). We assert structured error + residue cleanup.
        var bridge = new LegacyCommandBridge();
        var zipBytes = CreateZipWithEntries(
            ("foo.exe", Encoding.UTF8.GetBytes("MZ-dummy")),
            ("readme.txt", Encoding.UTF8.GetBytes("hi")));
        var args = JsonSerializer.SerializeToElement(new
        {
            data = new
            {
                name = "single.exe.zip",
                action = "upgrade",
                path = "/ignored",
                fileBase64 = Convert.ToBase64String(zipBytes)
            }
        });

        var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
        if (OperatingSystem.IsWindows())
        {
            // Launch may succeed or fail depending on the dummy; do not assert OS-specific launch.
            Assert.IsTrue(result.TryGetProperty("status", out _));
            return;
        }

        Assert.AreEqual("error", result.GetProperty("status").GetString());
        StringAssert.Contains(result.GetProperty("message").GetString(), "failed to launch patch executable");
        Assert.AreEqual(true, result.GetProperty("cleanedUp").GetBoolean());
        var path = result.GetProperty("path").GetString();
        Assert.IsFalse(Directory.Exists(path), "per-request patch dir should be cleaned after launch failure");
    }

    private static byte[] CreateZipWithEntries(params (string name, byte[] content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }
        }

        return ms.ToArray();
    }
}

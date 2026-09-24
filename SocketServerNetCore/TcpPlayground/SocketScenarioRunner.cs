using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public sealed class SocketScenarioRunner
{
    public async Task<SocketTestReport> RunAsync(SocketScenarioRunnerOptions options, CancellationToken cancellationToken = default)
    {
        var report = new SocketTestReport();
        var serverCertificate = options.ServerCertificate ?? DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            Port = options.Port,
            Backlog = options.Backlog,
            ServerCertificate = serverCertificate,
            AuthenticationSecret = options.AuthenticationSecret,
            AuthenticationTimeout = options.AuthenticationTimeout,
            DefaultCommandTimeout = options.DefaultCommandTimeout,
            DuplicateSessionPolicy = options.DuplicateSessionPolicy
        });

        await server.StartAsync(cancellationToken);
        report.ServerPort = server.Port;

        Console.WriteLine($"Started secure raw TCP server on 127.0.0.1:{server.Port} with TLS");

        await RunScenarioAsync(report, "authenticated heartbeat", async scenario =>
        {
            await using var client = CreateClient(options, server.Port, "heartbeat-client", DeviceRoles.Client, serverCertificate);
            await client.ConnectAsync(cancellationToken);

            var response = await client.SendRequestAsync("heartbeat", new { sequence = 1 }, envelope => envelope.Type == "heartbeat.ack", cancellationToken);
            Assert(response.Type == "heartbeat.ack", scenario, "Heartbeat acknowledgement was not returned.");
            CaptureTranscripts(scenario, client);
        }, cancellationToken);

        await RunScenarioAsync(report, "admin command fan-out with ack/result summary", async scenario =>
        {
            await using var admin = CreateClient(options, server.Port, "admin-1", DeviceRoles.Admin, serverCertificate);
            await using var alpha = CreateClient(options, server.Port, "alpha", DeviceRoles.Client, serverCertificate);
            await using var bravo = CreateClient(options, server.Port, "bravo", DeviceRoles.Client, serverCertificate);
            await Task.WhenAll(admin.ConnectAsync(cancellationToken), alpha.ConnectAsync(cancellationToken), bravo.ConnectAsync(cancellationToken));

            var accepted = await admin.SendRequestAsync("admin-command", new
            {
                commandId = "scenario-health-check",
                commandName = "health-check",
                targetMode = "all",
                timeoutMs = 1500
            }, envelope => envelope.Type == "admin-command.accepted", cancellationToken);

            var commandId = accepted.Payload?.GetProperty("commandId").GetString() ?? "scenario-health-check";
            var alphaCommand = await alpha.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId, cancellationToken: cancellationToken);
            var bravoCommand = await bravo.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId, cancellationToken: cancellationToken);

            await SendCommandAckAsync(alpha, commandId, cancellationToken);
            await SendCommandAckAsync(bravo, commandId, cancellationToken);
            await SendCommandResultAsync(alpha, commandId, "health-check", new { status = "ok", observedBy = "alpha" }, cancellationToken);
            await SendCommandResultAsync(bravo, commandId, "health-check", new { status = "ok", observedBy = "bravo" }, cancellationToken);

            var summary = await admin.WaitForMessageAsync(envelope => envelope.Type == "command-summary" && envelope.CorrelationId == commandId, cancellationToken: cancellationToken);
            Assert(summary.Payload?.GetProperty("completedDeviceIds").GetArrayLength() == 2, scenario, "Expected both clients to complete the command.");

            CaptureTranscripts(scenario, admin);
            CaptureTranscripts(scenario, alpha);
            CaptureTranscripts(scenario, bravo);
        }, cancellationToken);

        await RunScenarioAsync(report, "duplicate device rejection", async scenario =>
        {
            await using var original = CreateClient(options, server.Port, "shared-device", DeviceRoles.Client, serverCertificate);
            await using var duplicate = CreateClient(options, server.Port, "shared-device", DeviceRoles.Client, serverCertificate);
            await original.ConnectAsync(cancellationToken);
            try
            {
                await duplicate.ConnectAsync(cancellationToken);
                Assert(false, scenario, "Expected duplicate device authentication to fail.");
            }
            catch (AuthenticationException)
            {
            }

            CaptureTranscripts(scenario, original);
            CaptureTranscripts(scenario, duplicate);
        }, cancellationToken);

        await RunScenarioAsync(report, "malformed input is isolated", async scenario =>
        {
            await using var healthyClient = CreateClient(options, server.Port, "healthy-client", DeviceRoles.Client, serverCertificate);
            await healthyClient.ConnectAsync(cancellationToken);

            await SendMalformedMessageAsync(options, server.Port, serverCertificate, cancellationToken);

            var echo = await healthyClient.SendRequestAsync("echo", new { message = "still alive" }, envelope => envelope.Type == "echo.response", cancellationToken);
            Assert(echo.Payload?.GetProperty("message").GetString() == "still alive", scenario, "Healthy client traffic was disrupted.");
            CaptureTranscripts(scenario, healthyClient);
        }, cancellationToken);

        await RunScenarioAsync(report, "command timeout summary", async scenario =>
        {
            await using var admin = CreateClient(options, server.Port, "admin-timeout", DeviceRoles.Admin, serverCertificate);
            await using var client = CreateClient(options, server.Port, "slow-client", DeviceRoles.Client, serverCertificate);
            await Task.WhenAll(admin.ConnectAsync(cancellationToken), client.ConnectAsync(cancellationToken));

            var accepted = await admin.SendRequestAsync("admin-command", new
            {
                commandId = "scenario-timeout",
                commandName = "collect-diagnostics",
                targetMode = "devices",
                targetDeviceIds = new[] { "slow-client" },
                timeoutMs = 300
            }, envelope => envelope.Type == "admin-command.accepted", cancellationToken);

            var commandId = accepted.Payload?.GetProperty("commandId").GetString() ?? "scenario-timeout";
            var inbound = await client.WaitForMessageAsync(envelope => envelope.Type == "admin-command" && envelope.CorrelationId == commandId, cancellationToken: cancellationToken);
            Assert(inbound.Type == "admin-command", scenario, "Expected client command dispatch.");

            var summary = await admin.WaitForMessageAsync(envelope => envelope.Type == "command-summary" && envelope.CorrelationId == commandId, timeoutOverride: TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);
            Assert(summary.Payload?.GetProperty("timedOut").GetBoolean() == true, scenario, "Expected a timed out command summary.");
            CaptureTranscripts(scenario, admin);
            CaptureTranscripts(scenario, client);
        }, cancellationToken);

        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        report.ReportPath = Path.IsPathRooted(options.ReportPath)
            ? options.ReportPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.ReportPath));
        await WriteReportAsync(report, report.ReportPath, cancellationToken);
        return report;
    }

    private static TcpTestClient CreateClient(SocketScenarioRunnerOptions options, int port, string clientId, string role, X509Certificate2 serverCertificate)
        => new(new TcpTestClientOptions
        {
            ClientId = clientId,
            Role = role,
            Host = "127.0.0.1",
            Port = port,
            ConnectTimeout = options.ConnectTimeout,
            ResponseTimeout = options.ResponseTimeout,
            RetryCount = options.RetryCount,
            RetryDelay = options.RetryDelay,
            UseTls = options.UseTls,
            TlsTargetHost = options.TlsTargetHost,
            AllowUntrustedCertificates = options.AllowUntrustedCertificates,
            AuthenticationSecret = options.AuthenticationSecret,
            RemoteCertificateValidationCallback = CreateCertificateValidationCallback(serverCertificate)
        });

    private static async Task SendCommandAckAsync(TcpTestClient client, string commandId, CancellationToken cancellationToken)
        => await client.SendAsync(SocketEnvelope.Create(
            type: "command-ack",
            deviceId: client.ClientId,
            payload: new
            {
                commandId,
                status = "accepted"
            },
            role: client.Role,
            correlationId: commandId), cancellationToken);

    private static async Task SendCommandResultAsync(TcpTestClient client, string commandId, string commandName, object result, CancellationToken cancellationToken)
        => await client.SendAsync(SocketEnvelope.Create(
            type: "command-result",
            deviceId: client.ClientId,
            payload: new
            {
                commandId,
                commandName,
                status = "completed",
                success = true,
                result
            },
            role: client.Role,
            correlationId: commandId), cancellationToken);

    private static async Task RunScenarioAsync(SocketTestReport report, string name, Func<ScenarioResult, Task> action, CancellationToken cancellationToken)
    {
        var scenario = new ScenarioResult { Name = name, Passed = true };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action(scenario);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            scenario.Passed = false;
            scenario.Failures.Add(exception.Message);
            scenario.Diagnostics.Add(exception.ToString());
        }
        finally
        {
            stopwatch.Stop();
            scenario.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            scenario.Passed = scenario.Passed && scenario.Failures.Count == 0;
            report.Scenarios.Add(scenario);
            Console.WriteLine($"[{(scenario.Passed ? "PASS" : "FAIL")}] {scenario.Name} ({scenario.DurationMs:N0} ms)");
            foreach (var failure in scenario.Failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }
    }

    private static async Task WriteReportAsync(SocketTestReport report, string configuredPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configuredPath)!);
        await File.WriteAllTextAsync(configuredPath, JsonSerializer.Serialize(report, JsonLineSocketProtocol.SerializerOptions), cancellationToken);
    }

    private static void CaptureTranscripts(ScenarioResult scenario, TcpTestClient client)
        => scenario.ClientTranscripts[client.ClientId] = client.SnapshotTranscript().ToList();

    private static void Assert(bool condition, ScenarioResult scenario, string message)
    {
        if (!condition)
        {
            scenario.Passed = false;
            scenario.Failures.Add(message);
        }
    }

    private static Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool> CreateCertificateValidationCallback(X509Certificate2 certificate)
    {
        var expectedThumbprint = certificate.Thumbprint;
        return (presentedCertificate, _, _) =>
            presentedCertificate is not null
            && string.Equals(presentedCertificate.Thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase);
    }

    private static RemoteCertificateValidationCallback CreateRemoteCertificateValidationCallback(X509Certificate2 certificate)
    {
        var expectedThumbprint = certificate.Thumbprint;
        return (_, presentedCertificate, chain, sslPolicyErrors) =>
        {
            if (presentedCertificate is null)
            {
                return false;
            }

            using var presented = new X509Certificate2(presentedCertificate.GetRawCertData());
            return string.Equals(presented.Thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase);
        };
    }

    private static async Task SendMalformedMessageAsync(SocketScenarioRunnerOptions options, int port, X509Certificate2 serverCertificate, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port, cancellationToken);

        Stream stream = client.GetStream();
        if (options.UseTls)
        {
            var sslStream = new SslStream(stream, leaveInnerStreamOpen: false, CreateRemoteCertificateValidationCallback(serverCertificate));
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = options.TlsTargetHost,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, cancellationToken);
            stream = sslStream;
        }

        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false) { AutoFlush = true, NewLine = "\n" };
        await writer.WriteLineAsync("{ definitely-not-json }");
    }
}

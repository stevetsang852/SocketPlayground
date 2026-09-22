using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public sealed class SocketScenarioRunner
{
    public async Task<SocketTestReport> RunAsync(SocketScenarioRunnerOptions options, CancellationToken cancellationToken = default)
    {
        var report = new SocketTestReport();
        await using var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            Port = options.Port,
            Backlog = options.Backlog
        });

        await server.StartAsync(cancellationToken);
        report.ServerPort = server.Port;

        Console.WriteLine($"Started raw TCP server on 127.0.0.1:{server.Port}");

        await RunScenarioAsync(report, "echo/round-trip validation", async scenario =>
        {
            await using var client = CreateClient(options, server.Port, "echo-client");
            await client.ConnectAsync(cancellationToken);
            var response = await client.SendRequestAsync(
                "echo",
                new { message = "hello tcp playground", ordinal = 1 },
                envelope => envelope.Type == "echo.response",
                cancellationToken);

            var payload = response.DeserializePayload<EchoPayload>();
            Assert(payload?.Message == "hello tcp playground", scenario, "Echo payload did not round-trip.");
            CaptureTranscripts(scenario, client);
        }, cancellationToken);

        await RunScenarioAsync(report, "concurrent broadcast or fan-out validation", async scenario =>
        {
            var clients = new[]
            {
                CreateClient(options, server.Port, "alpha"),
                CreateClient(options, server.Port, "bravo"),
                CreateClient(options, server.Port, "charlie")
            };

            try
            {
                await Task.WhenAll(clients.Select(client => client.ConnectAsync(cancellationToken)));
                var sender = clients[0];
                var request = SocketEnvelope.Create("broadcast", sender.ClientId, new { message = "sync" });
                await sender.SendAsync(request, cancellationToken);

                var messages = await Task.WhenAll(clients.Select(client => client.WaitForMessageAsync(
                    envelope => envelope.Type == "broadcast.event" && envelope.RequestId == request.RequestId,
                    cancellationToken: cancellationToken)));

                Assert(messages.Length == 3, scenario, "Not every connected client received the broadcast.");
                foreach (var client in clients)
                {
                    CaptureTranscripts(scenario, client);
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    await client.DisposeAsync();
                }
            }
        }, cancellationToken);

        await RunScenarioAsync(report, "disconnect/reconnect behavior", async scenario =>
        {
            await using var client = CreateClient(options, server.Port, "reconnect-client");
            await client.ConnectAsync(cancellationToken);
            await client.DisconnectAsync();
            await client.ConnectAsync(cancellationToken);
            var response = await client.SendRequestAsync("echo", new { message = "after reconnect" }, envelope => envelope.Type == "echo.response", cancellationToken);
            var payload = response.DeserializePayload<EchoPayload>();
            Assert(payload?.Message == "after reconnect", scenario, "Reconnected client failed to receive an echo response.");
            CaptureTranscripts(scenario, client);
        }, cancellationToken);

        await RunScenarioAsync(report, "malformed input is rejected or isolated", async scenario =>
        {
            await using var goodClient = CreateClient(options, server.Port, "good-client");
            await goodClient.ConnectAsync(cancellationToken);

            using var badClient = new TcpClient();
            await badClient.ConnectAsync("127.0.0.1", server.Port, cancellationToken);
            await using (var writer = new StreamWriter(badClient.GetStream(), new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" })
            {
                await writer.WriteLineAsync("{ definitely-not-json }");
            }

            var response = await goodClient.SendRequestAsync("echo", new { message = "still alive" }, envelope => envelope.Type == "echo.response", cancellationToken);
            var payload = response.DeserializePayload<EchoPayload>();
            Assert(payload?.Message == "still alive", scenario, "A malformed client disrupted healthy client traffic.");
            CaptureTranscripts(scenario, goodClient);
        }, cancellationToken);

        await RunScenarioAsync(report, "timeout/error reporting", async scenario =>
        {
            await using var client = CreateClient(options, server.Port, "timeout-client", responseTimeout: TimeSpan.FromMilliseconds(250));
            await client.ConnectAsync(cancellationToken);

            var error = await client.SendRequestAsync("unknown", null, envelope => envelope.Type == "error", cancellationToken);
            Assert(error.Type == "error", scenario, "Unknown message types should return a structured error.");

            try
            {
                await client.SendRequestAsync(
                    "delay",
                    new { delayMs = 1000, message = "slow" },
                    envelope => envelope.Type == "delay.response",
                    cancellationToken,
                    timeoutOverride: TimeSpan.FromMilliseconds(250));
                Assert(false, scenario, "Expected a timeout while waiting for a delayed response.");
            }
            catch (TimeoutException exception)
            {
                scenario.Diagnostics.Add($"Observed expected timeout: {exception.Message}");
            }

            CaptureTranscripts(scenario, client);
        }, cancellationToken);

        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        report.ReportPath = Path.IsPathRooted(options.ReportPath)
            ? options.ReportPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.ReportPath));
        await WriteReportAsync(report, report.ReportPath, cancellationToken);
        return report;
    }

    private static TcpTestClient CreateClient(SocketScenarioRunnerOptions options, int port, string clientId, TimeSpan? responseTimeout = null)
        => new(new TcpTestClientOptions
        {
            ClientId = clientId,
            Host = "127.0.0.1",
            Port = port,
            ConnectTimeout = options.ConnectTimeout,
            ResponseTimeout = responseTimeout ?? options.ResponseTimeout,
            RetryCount = options.RetryCount,
            RetryDelay = options.RetryDelay
        });

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

    private sealed class EchoPayload
    {
        public string? Message { get; set; }
    }
}

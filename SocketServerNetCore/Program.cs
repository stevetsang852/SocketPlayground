using SocketServerNetCore.TcpPlayground;

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var options = CommandLineOptions.Parse(args);

try
{
    return options.Mode switch
    {
        PlaygroundMode.Server => await RunServerAsync(options, cancellation.Token),
        PlaygroundMode.Scenario => await RunScenarioAsync(options, cancellation.Token),
        _ => ShowUsage()
    };
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled.");
    return 1;
}

static async Task<int> RunServerAsync(CommandLineOptions options, CancellationToken cancellationToken)
{
    var serverOptions = new TcpPlaygroundServerOptions
    {
        Port = options.Port,
        Backlog = options.Backlog
    };

    await using var server = new TcpPlaygroundServer(serverOptions);
    await server.StartAsync(cancellationToken);

    Console.WriteLine($"TCP playground server listening on 127.0.0.1:{server.Port}");
    Console.WriteLine("Press Ctrl+C to stop.");

    try
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
    catch (OperationCanceledException)
    {
    }

    await server.StopAsync(CancellationToken.None);
    return 0;
}

static async Task<int> RunScenarioAsync(CommandLineOptions options, CancellationToken cancellationToken)
{
    var report = await new SocketScenarioRunner().RunAsync(new SocketScenarioRunnerOptions
    {
        Port = options.Port,
        Backlog = options.Backlog,
        ReportPath = options.ReportPath,
        ConnectTimeout = TimeSpan.FromMilliseconds(options.ConnectTimeoutMs),
        ResponseTimeout = TimeSpan.FromMilliseconds(options.ResponseTimeoutMs),
        RetryCount = options.RetryCount,
        RetryDelay = TimeSpan.FromMilliseconds(options.RetryDelayMs)
    }, cancellationToken);

    Console.WriteLine();
    Console.WriteLine($"JSON report written to {report.ReportPath}");
    return report.AllPassed ? 0 : 1;
}

static int ShowUsage()
{
    Console.WriteLine("SocketServerNetCore raw TCP playground");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- server [--port 11000]");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- scenario [--report artifacts/socket-playground-report.json]");
    Console.WriteLine();
    Console.WriteLine("Modes:");
    Console.WriteLine("  server    Starts the raw TCP server on loopback.");
    Console.WriteLine("  scenario  Runs the automated client scenarios and writes a JSON report.");
    return 1;
}

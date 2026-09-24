using SocketServerNetCore.TcpPlayground;
using System.Security.Cryptography.X509Certificates;

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
        PlaygroundMode.IssueToken => RunIssueToken(options),
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
        Backlog = options.Backlog,
        ServerCertificate = ResolveServerCertificate(options),
        AuthenticationSecret = ResolveAuthenticationSecret(options),
        AuthenticationTimeout = TimeSpan.FromMilliseconds(options.AuthenticationTimeoutMs),
        DefaultCommandTimeout = TimeSpan.FromMilliseconds(options.CommandTimeoutMs),
        DuplicateSessionPolicy = CommandProtocol.ParseDuplicateSessionPolicy(options.DuplicatePolicy)
    };

    await using var server = new TcpPlaygroundServer(serverOptions);
    await server.StartAsync(cancellationToken);

    Console.WriteLine($"TCP playground server listening on 127.0.0.1:{server.Port} with TLS");

    try
    {
        if (Console.IsInputRedirected)
        {
            Console.WriteLine("Stdin is redirected; interactive console disabled. Press Ctrl+C to stop.");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        else
        {
            Console.WriteLine("Use the console to list devices and send allowlisted admin commands.");
            Console.WriteLine("Press Ctrl+C or type quit to stop.");
            await ServerConsole.RunAsync(server, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
    }

    await server.StopAsync(CancellationToken.None);
    return 0;
}

static async Task<int> RunScenarioAsync(CommandLineOptions options, CancellationToken cancellationToken)
{
    var serverCertificate = ResolveServerCertificate(options);
    var report = await new SocketScenarioRunner().RunAsync(new SocketScenarioRunnerOptions
    {
        Port = options.Port,
        Backlog = options.Backlog,
        ReportPath = options.ReportPath,
        ConnectTimeout = TimeSpan.FromMilliseconds(options.ConnectTimeoutMs),
        ResponseTimeout = TimeSpan.FromMilliseconds(options.ResponseTimeoutMs),
        RetryCount = options.RetryCount,
        RetryDelay = TimeSpan.FromMilliseconds(options.RetryDelayMs),
        UseTls = options.TlsEnabled,
        AllowUntrustedCertificates = options.AllowUntrustedCertificates,
        ServerCertificate = serverCertificate,
        AuthenticationSecret = ResolveAuthenticationSecret(options),
        AuthenticationTimeout = TimeSpan.FromMilliseconds(options.AuthenticationTimeoutMs),
        DefaultCommandTimeout = TimeSpan.FromMilliseconds(options.CommandTimeoutMs),
        DuplicateSessionPolicy = CommandProtocol.ParseDuplicateSessionPolicy(options.DuplicatePolicy)
    }, cancellationToken);

    Console.WriteLine();
    Console.WriteLine($"JSON report written to {report.ReportPath}");
    return report.AllPassed ? 0 : 1;
}

static int ShowUsage()
{
    Console.WriteLine("SocketServerNetCore secure raw TCP playground");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- server [--port 11000] [--auth-secret <value>]");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- scenario [--report artifacts/socket-playground-report.json] [--auth-secret <value>]");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- issue-token --device-id agent-1 --role client --auth-secret <value>");
    Console.WriteLine();
    Console.WriteLine("Modes:");
    Console.WriteLine("  server       Starts the TLS-protected raw TCP command broker on loopback.");
    Console.WriteLine("  scenario     Runs automated authenticated client scenarios and writes a JSON report.");
    Console.WriteLine("  issue-token  Prints a short-lived HMAC-signed token for a device/role pair.");
    Console.WriteLine();
    Console.WriteLine("Server console commands:");
    Console.WriteLine("  help / list / send <command> [all|<deviceId>] / quit");
    Console.WriteLine();
    Console.WriteLine("Security options:");
    Console.WriteLine("  --tls true                   Enables SslStream over the raw TCP transport (required).");
    Console.WriteLine("  --tls-cert-path <path>       Loads a PFX development certificate for the server.");
    Console.WriteLine("  --tls-cert-password <value>  Password for the PFX file.");
    Console.WriteLine("  --auth-secret <value>        Shared development secret used to sign short-lived tokens.");
    Console.WriteLine("  --auth-timeout-ms <value>    Authentication deadline in milliseconds.");
    Console.WriteLine("  --command-timeout-ms <value> Default command result timeout in milliseconds.");
    Console.WriteLine("  --duplicate-policy <value>   reject-new (default) or replace-existing.");
    Console.WriteLine("  --allow-untrusted true       Allows self-signed development certificates for manual clients.");
    return 1;
}

static X509Certificate2? ResolveServerCertificate(CommandLineOptions options)
{
    if (!options.TlsEnabled)
    {
        throw new InvalidOperationException("TLS is required for this command-and-control server.");
    }

    return string.IsNullOrWhiteSpace(options.TlsCertPath)
        ? DevelopmentCertificateLoader.CreateLoopbackCertificate()
        : DevelopmentCertificateLoader.LoadFromFile(options.TlsCertPath, options.TlsCertPassword);
}

static string ResolveAuthenticationSecret(CommandLineOptions options)
{
    if (string.IsNullOrWhiteSpace(options.AuthenticationSecret))
    {
        throw new InvalidOperationException("Provide --auth-secret or set SOCKET_PLAYGROUND_AUTH_SECRET.");
    }

    return options.AuthenticationSecret;
}

static int RunIssueToken(CommandLineOptions options)
{
    var token = CommandAuthTokenService.CreateToken(
        ResolveAuthenticationSecret(options),
        options.DeviceId,
        options.Role,
        DateTimeOffset.UtcNow.AddMinutes(options.TokenLifetimeMinutes));
    Console.WriteLine(token);
    return 0;
}

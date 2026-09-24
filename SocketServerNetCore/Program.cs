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
    var serverOptions = CreateServerOptions(options);
    await using var server = new TcpPlaygroundServer(serverOptions);
    await server.StartAsync(cancellationToken);

    Console.WriteLine($"TCP playground server listening on 127.0.0.1:{server.Port} with TLS");
    if (!string.IsNullOrWhiteSpace(options.AdminUsername))
    {
        Console.WriteLine("Admin login enabled. After TLS connect, send a login command with role=admin.");
    }

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

static TcpPlaygroundServerOptions CreateServerOptions(CommandLineOptions options)
    => new()
    {
        Port = options.Port,
        Backlog = options.Backlog,
        ServerCertificate = ResolveServerCertificate(options),
        AuthenticationSecret = ResolveAuthenticationSecret(options),
        AdminUsername = options.AdminUsername,
        AdminPassword = options.AdminPassword,
        RequireClientCertificate = options.RequireClientCertificate,
        RequireValidClientCertificate = options.RequireValidClientCertificate,
        AuthenticationTimeout = TimeSpan.FromMilliseconds(options.AuthenticationTimeoutMs),
        DefaultCommandTimeout = TimeSpan.FromMilliseconds(options.CommandTimeoutMs),
        DuplicateSessionPolicy = CommandProtocol.ParseDuplicateSessionPolicy(options.DuplicatePolicy)
    };

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
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- server [--port 11000] [--auth-secret <value>] [--admin-user admin] [--admin-password <value>]");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- scenario [--report artifacts/socket-playground-report.json] [--auth-secret <value>]");
    Console.WriteLine("  dotnet run --project SocketServerNetCore -- issue-token --device-id agent-1 --role client --auth-secret <value>");
    Console.WriteLine();
    Console.WriteLine("After TLS connect, clients may send either authenticate or login.");
    Console.WriteLine("login payload: { deviceId, username, password, role }");
    Console.WriteLine("role=admin requires --admin-user / --admin-password.");
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

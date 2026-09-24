namespace SocketServerNetCore.TcpPlayground;

public static class ServerConsole
{
    public static async Task RunAsync(TcpPlaygroundServer server, CancellationToken cancellationToken)
    {
        PrintHelp();
        Console.WriteLine("Server console is ready. Type 'help' for commands.");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("server> ");
            var line = await ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            var command = line.Trim();
            if (command.Length == 0)
            {
                continue;
            }

            try
            {
                await HandleAsync(server, command, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private static async Task HandleAsync(TcpPlaygroundServer server, string command, CancellationToken cancellationToken)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].ToLowerInvariant();

        switch (verb)
        {
            case "help":
            case "?":
                PrintHelp();
                break;
            case "list":
                PrintDevices(server.GetAuthenticatedDevices());
                break;
            case "send":
                await SendAsync(server, parts, cancellationToken);
                break;
            case "quit":
            case "exit":
                throw new OperationCanceledException();
            default:
                Console.WriteLine($"Unknown command '{verb}'. Type 'help'.");
                break;
        }
    }

    private static async Task SendAsync(TcpPlaygroundServer server, IReadOnlyList<string> parts, CancellationToken cancellationToken)
    {
        if (parts.Count < 2)
        {
            Console.WriteLine("Usage: send <command> [all|<deviceId>]");
            return;
        }

        var commandName = parts[1];
        var target = parts.Count >= 3 ? parts[2] : "all";
        var targetMode = string.Equals(target, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "devices";
        var targetDeviceIds = targetMode == "devices" ? new[] { target } : Array.Empty<string>();

        Console.WriteLine($"Dispatching '{commandName}' to {target}...");
        await server.DispatchConsoleCommandAsync(commandName, targetMode, targetDeviceIds, timeoutMs: 3000, cancellationToken);
    }

    private static void PrintDevices(IReadOnlyList<AuthenticatedDeviceInfo> devices)
    {
        if (devices.Count == 0)
        {
            Console.WriteLine("No authenticated devices.");
            return;
        }

        Console.WriteLine($"Authenticated devices ({devices.Count}):");
        foreach (var device in devices)
        {
            var heartbeat = device.LastHeartbeatUtc?.ToString("O") ?? "-";
            Console.WriteLine($"- {device.DeviceId} role={device.Role} heartbeat={heartbeat} until={device.AuthenticatedUntilUtc:O}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Console commands:");
        Console.WriteLine("  help                              Show this help");
        Console.WriteLine("  list                              List authenticated devices");
        Console.WriteLine("  send <command> [all|<deviceId>]   Dispatch an allowlisted admin command");
        Console.WriteLine("  quit                              Stop the server");
        Console.WriteLine();
        Console.WriteLine("Allowlisted commands:");
        Console.WriteLine("  " + string.Join(", ", CommandProtocol.AllowedCommands.OrderBy(command => command)));
    }

    private static Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        return Task.Run(Console.ReadLine, cancellationToken);
    }
}

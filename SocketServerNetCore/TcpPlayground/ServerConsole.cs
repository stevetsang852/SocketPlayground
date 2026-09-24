using System.Text.Json;
using System.Text.RegularExpressions;

namespace SocketServerNetCore.TcpPlayground;

public static class ServerConsole
{
    private static readonly Regex SendPattern = new(
        @"^send\s+(?<cmd>\S+)\s+(?<target>\S+)(?:\s+(?<args>[\s\S]+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static async Task RunAsync(TcpPlaygroundServer server, CancellationToken cancellationToken)
    {
        PrintHelp(server);
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
                PrintHelp(server);
                break;
            case "list":
                PrintDevices(server.GetAuthenticatedDevices());
                break;
            case "send":
                await SendAsync(server, command, cancellationToken);
                break;
            case "quit":
            case "exit":
                throw new OperationCanceledException();
            default:
                Console.WriteLine($"Unknown command '{verb}'. Type 'help'.");
                break;
        }
    }

    private static async Task SendAsync(TcpPlaygroundServer server, string rawCommand, CancellationToken cancellationToken)
    {
        var match = SendPattern.Match(rawCommand.Trim());
        if (!match.Success)
        {
            Console.WriteLine("Usage: send <command> [all|<deviceId>] [json-arguments]");
            return;
        }

        var commandName = match.Groups["cmd"].Value;
        var target = match.Groups["target"].Value;
        var targetMode = string.Equals(target, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "devices";
        var targetDeviceIds = targetMode == "devices" ? new[] { target } : Array.Empty<string>();
        JsonElement? arguments = null;

        if (match.Groups["args"].Success && !string.IsNullOrWhiteSpace(match.Groups["args"].Value))
        {
            try
            {
                arguments = JsonSerializer.Deserialize<JsonElement>(match.Groups["args"].Value.Trim());
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Invalid JSON arguments: {ex.Message}");
                return;
            }
        }

        Console.WriteLine($"Dispatching '{commandName}' to {target}...");
        await server.DispatchConsoleCommandAsync(
            commandName,
            targetMode,
            targetDeviceIds,
            timeoutMs: 3000,
            arguments: arguments,
            cancellationToken: cancellationToken);
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

    private static void PrintHelp(TcpPlaygroundServer server)
    {
        Console.WriteLine("Console commands:");
        Console.WriteLine("  help                                            Show this help");
        Console.WriteLine("  list                                            List authenticated devices");
        Console.WriteLine("  send <command> [all|<deviceId>] [json-arguments] Dispatch an allowlisted admin command");
        Console.WriteLine("  quit                                            Stop the server");
        Console.WriteLine();
        Console.WriteLine("Allowlisted commands (this server):");
        Console.WriteLine("  " + string.Join(", ", server.EffectiveAllowedCommands.OrderBy(command => command)));
        if (!server.AllowLegacyCommands)
        {
            Console.WriteLine("Legacy commands (csharp/upload/wallpapertaskpack) are OFF. Restart with --allow-legacy-commands true to enable.");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Legacy bridge examples:");
            Console.WriteLine("  send wallpapertaskpack all {\"data\":\"rname\"}");
            Console.WriteLine("  send upload all {\"data\":{\"name\":\"a.txt\",\"action\":\"upload\",\"path\":\"/tmp\",\"fileBase64\":\"YQ==\"}}");
        }
    }

    private static Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        return Task.Run(Console.ReadLine, cancellationToken);
    }
}

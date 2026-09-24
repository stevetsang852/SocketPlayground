namespace Payload;

public static class CanonicalAgentHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        var host = Get(args, "--host") ?? "127.0.0.1";
        var port = int.Parse(Get(args, "--port") ?? "11000");
        var deviceId = Get(args, "--device-id") ?? "dotnet-agent-1";
        var user = Get(args, "--login-user") ?? deviceId;
        var password = Get(args, "--login-password") ?? Environment.GetEnvironmentVariable("SOCKET_PLAYGROUND_AUTH_SECRET") ?? "";

        using var client = new CanonicalTcpClient(host, port, deviceId, "client");
        var authenticated = await client.ConnectAndLoginAsync(user, password);
        Console.WriteLine(authenticated);
        Console.WriteLine("Canonical TLS agent connected. Waiting for allowlisted commands. Ctrl+C to exit.");
        await client.ProcessCommandsAsync();
        return 0;
    }

    private static string? Get(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

namespace SocketServerNetCore.TcpPlayground;

public enum PlaygroundMode
{
    Scenario,
    Server,
    IssueToken,
    Help
}

public sealed class CommandLineOptions
{
    public PlaygroundMode Mode { get; init; } = PlaygroundMode.Scenario;
    public int Port { get; init; }
    public int Backlog { get; init; } = 50;
    public string BindAddress { get; init; } = "127.0.0.1";
    public string ReportPath { get; init; } = Path.Combine("artifacts", "socket-playground-report.json");
    public int ConnectTimeoutMs { get; init; } = 2000;
    public int ResponseTimeoutMs { get; init; } = 2000;
    public int RetryCount { get; init; } = 1;
    public int RetryDelayMs { get; init; } = 250;
    public bool TlsEnabled { get; init; } = true;
    public bool AllowUntrustedCertificates { get; init; }
    public string? TlsCertPath { get; init; }
    public string? TlsCertPassword { get; init; }
    public string AuthenticationSecret { get; init; } = string.Empty;
    public string? AdminUsername { get; init; }
    public string? AdminPassword { get; init; }
    public bool RequireClientCertificate { get; init; }
    public bool RequireValidClientCertificate { get; init; }
    public int AuthenticationTimeoutMs { get; init; } = 5000;
    public int CommandTimeoutMs { get; init; } = 2000;
    public string DuplicatePolicy { get; init; } = "reject-new";
    public string DeviceId { get; init; } = "sample-device";
    public string Role { get; init; } = DeviceRoles.Client;
    public int TokenLifetimeMinutes { get; init; } = 10;

    public static CommandLineOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandLineOptions();
        }

        var mode = args[0].ToLowerInvariant() switch
        {
            "server" => PlaygroundMode.Server,
            "scenario" => PlaygroundMode.Scenario,
            "issue-token" => PlaygroundMode.IssueToken,
            "help" or "--help" or "-h" => PlaygroundMode.Help,
            _ => PlaygroundMode.Help
        };

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index++)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index == args.Length - 1)
            {
                continue;
            }

            values[key[2..]] = args[++index];
        }

        return new CommandLineOptions
        {
            Mode = mode,
            Port = GetInt(values, "port", 0),
            Backlog = GetInt(values, "backlog", 50),
            BindAddress = values.TryGetValue("bind", out var bind)
                ? bind
                : Environment.GetEnvironmentVariable("SOCKET_PLAYGROUND_BIND") ?? "127.0.0.1",
            ReportPath = values.TryGetValue("report", out var reportPath)
                ? reportPath
                : Path.Combine("artifacts", "socket-playground-report.json"),
            ConnectTimeoutMs = GetInt(values, "connect-timeout-ms", 2000),
            ResponseTimeoutMs = GetInt(values, "response-timeout-ms", 2000),
            RetryCount = GetInt(values, "retry-count", 1),
            RetryDelayMs = GetInt(values, "retry-delay-ms", 250),
            TlsEnabled = GetBool(values, "tls", true),
            AllowUntrustedCertificates = GetBool(values, "allow-untrusted", false),
            TlsCertPath = values.TryGetValue("tls-cert-path", out var certPath) ? certPath : null,
            TlsCertPassword = values.TryGetValue("tls-cert-password", out var certPassword) ? certPassword : null,
            AuthenticationSecret = values.TryGetValue("auth-secret", out var authSecret)
                ? authSecret
                : Environment.GetEnvironmentVariable("SOCKET_PLAYGROUND_AUTH_SECRET") ?? string.Empty,
            AdminUsername = values.TryGetValue("admin-user", out var adminUser)
                ? adminUser
                : Environment.GetEnvironmentVariable("SOCKET_PLAYGROUND_ADMIN_USER"),
            AdminPassword = values.TryGetValue("admin-password", out var adminPassword)
                ? adminPassword
                : Environment.GetEnvironmentVariable("SOCKET_PLAYGROUND_ADMIN_PASSWORD"),
            RequireClientCertificate = GetBool(values, "require-client-cert", false),
            RequireValidClientCertificate = GetBool(values, "require-valid-client-cert", false),
            AuthenticationTimeoutMs = GetInt(values, "auth-timeout-ms", 5000),
            CommandTimeoutMs = GetInt(values, "command-timeout-ms", 2000),
            DuplicatePolicy = values.TryGetValue("duplicate-policy", out var duplicatePolicy)
                ? duplicatePolicy
                : "reject-new",
            DeviceId = values.TryGetValue("device-id", out var deviceId) ? deviceId : "sample-device",
            Role = values.TryGetValue("role", out var role) ? role : DeviceRoles.Client,
            TokenLifetimeMinutes = GetInt(values, "token-lifetime-minutes", 10)
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
        => values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
}

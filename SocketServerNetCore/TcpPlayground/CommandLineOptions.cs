using System.Globalization;

namespace SocketServerNetCore.TcpPlayground;

public enum PlaygroundMode
{
    Scenario,
    Server,
    Help
}

public sealed class CommandLineOptions
{
    public PlaygroundMode Mode { get; init; } = PlaygroundMode.Scenario;
    public int Port { get; init; }
    public int Backlog { get; init; } = 50;
    public string ReportPath { get; init; } = Path.Combine("artifacts", "socket-playground-report.json");
    public int ConnectTimeoutMs { get; init; } = 2000;
    public int ResponseTimeoutMs { get; init; } = 2000;
    public int RetryCount { get; init; } = 1;
    public int RetryDelayMs { get; init; } = 250;
    public bool TlsEnabled { get; init; }
    public bool AllowUntrustedCertificates { get; init; }
    public string? TlsCertPath { get; init; }
    public string? TlsCertPassword { get; init; }

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
            ReportPath = values.TryGetValue("report", out var reportPath)
                ? reportPath
                : Path.Combine("artifacts", "socket-playground-report.json"),
            ConnectTimeoutMs = GetInt(values, "connect-timeout-ms", 2000),
            ResponseTimeoutMs = GetInt(values, "response-timeout-ms", 2000),
            RetryCount = GetInt(values, "retry-count", 1),
            RetryDelayMs = GetInt(values, "retry-delay-ms", 250),
            TlsEnabled = GetBool(values, "tls", false),
            AllowUntrustedCertificates = GetBool(values, "allow-untrusted", false),
            TlsCertPath = values.TryGetValue("tls-cert-path", out var certPath) ? certPath : null,
            TlsCertPassword = values.TryGetValue("tls-cert-password", out var certPassword) ? certPassword : null
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

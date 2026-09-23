using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public sealed class SocketScenarioRunnerOptions
{
    public int Port { get; init; }
    public int Backlog { get; init; } = 50;
    public string ReportPath { get; init; } = Path.Combine("artifacts", "socket-playground-report.json");
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan ResponseTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int RetryCount { get; init; } = 1;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);
    public bool UseTls { get; init; }
    public string TlsTargetHost { get; init; } = "localhost";
    public bool AllowUntrustedCertificates { get; init; }
    public X509Certificate2? ServerCertificate { get; init; }
}

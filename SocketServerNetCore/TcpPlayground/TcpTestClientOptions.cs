using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpTestClientOptions
{
    public string ClientId { get; init; } = $"client-{Guid.NewGuid():N}";
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; }
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan ResponseTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int RetryCount { get; init; } = 1;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);
    public bool UseTls { get; init; }
    public string TlsTargetHost { get; init; } = "localhost";
    public bool AllowUntrustedCertificates { get; init; }
    public Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? RemoteCertificateValidationCallback { get; init; }
}

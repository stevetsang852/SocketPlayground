using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpPlaygroundServerOptions
{
    public int Port { get; init; }
    public int Backlog { get; init; } = 50;
    public X509Certificate2? ServerCertificate { get; init; }
    public string AuthenticationSecret { get; init; } = string.Empty;
    public TimeSpan AuthenticationTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan DefaultCommandTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public DuplicateSessionPolicy DuplicateSessionPolicy { get; init; } = DuplicateSessionPolicy.RejectNew;
}

using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpPlaygroundServerOptions
{
    public int Port { get; init; }
    public int Backlog { get; init; } = 50;
    public X509Certificate2? ServerCertificate { get; init; }
}

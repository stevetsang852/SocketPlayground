namespace SocketServerNetCore.TcpPlayground;

public sealed class TcpPlaygroundServerOptions
{
    public int Port { get; init; }
    public int Backlog { get; init; } = 50;
}

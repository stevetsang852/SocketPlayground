using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SocketServerNetCore.TcpPlayground;

namespace SocketServerNetCore.Tests;

[TestClass]
public sealed class LoginAndCertificateTests
{
    private const string AuthSecret = "integration-test-secret";
    private const string AdminUser = "admin";
    private const string AdminPassword = "admin-pass";

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    [TestCategory("Login")]
    public async Task ClientMustCompleteTlsBeforeLoginAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartServerAsync(certificate);
        await using var client = CreateLoginClient(server.Port, certificate, "login-admin", autoAuthenticate: false);

        await client.ConnectAsync();

        Assert.IsTrue(client.IsTlsAuthenticated);
        Assert.IsTrue(client.IsEncryptedTransport);
        Assert.IsTrue(client.SnapshotTranscript().Any(item => item.MessageType == "tls.handshake"));
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    [TestCategory("Login")]
    public async Task LoginAsAdminAfterTlsConnectAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartServerAsync(certificate);
        await using var admin = CreateLoginClient(server.Port, certificate, "login-admin", autoAuthenticate: false);
        await using var agent = CreateTokenClient(server.Port, certificate, "login-agent");

        await admin.ConnectAsync();
        await agent.ConnectAsync();

        var authenticated = await admin.SendRequestAsync("login", new
        {
            deviceId = "login-admin",
            username = AdminUser,
            password = AdminPassword,
            role = DeviceRoles.Admin
        }, envelope => envelope.Type == "authenticated" || envelope.Type == "error");

        Assert.AreEqual("authenticated", authenticated.Type);
        Assert.AreEqual(DeviceRoles.Admin, authenticated.Payload?.GetProperty("role").GetString());
        Assert.AreEqual("login", authenticated.Payload?.GetProperty("method").GetString());

        var accepted = await admin.SendRequestAsync("admin-command", new
        {
            commandId = "login-health",
            commandName = "health-check",
            targetMode = "devices",
            targetDeviceIds = new[] { "login-agent" },
            timeoutMs = 1000
        }, envelope => envelope.Type == "admin-command.accepted");

        Assert.AreEqual("admin-command.accepted", accepted.Type);
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Login")]
    public async Task InvalidAdminLoginIsRejectedAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartServerAsync(certificate);
        await using var client = CreateLoginClient(server.Port, certificate, "bad-admin", autoAuthenticate: false);

        await client.ConnectAsync();
        var error = await client.SendRequestAsync("login", new
        {
            deviceId = "bad-admin",
            username = AdminUser,
            password = "wrong-password",
            role = DeviceRoles.Admin
        }, envelope => envelope.Type == "error");

        Assert.AreEqual("login_invalid", error.Payload?.GetProperty("code").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Login")]
    public async Task ClientLoginWithSharedSecretWorksAsync()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        await using var server = await StartServerAsync(certificate);
        await using var client = CreateLoginClient(server.Port, certificate, "login-client", autoAuthenticate: false);

        await client.ConnectAsync();
        var authenticated = await client.SendRequestAsync("login", new
        {
            deviceId = "login-client",
            username = "login-client",
            password = AuthSecret,
            role = DeviceRoles.Client
        }, envelope => envelope.Type == "authenticated" || envelope.Type == "error");

        Assert.AreEqual("authenticated", authenticated.Type);
        Assert.AreEqual(DeviceRoles.Client, authenticated.Payload?.GetProperty("role").GetString());
    }

    [TestMethod]
    [TestCategory("TcpPlayground")]
    [TestCategory("Tls")]
    public void CertificateInspectorReadsServerCertificate()
    {
        using var certificate = DevelopmentCertificateLoader.CreateLoopbackCertificate();
        var inspection = CertificateInspector.Inspect(certificate);

        Assert.IsTrue(inspection.HasCertificate);
        Assert.IsFalse(inspection.IsExpired);
        Assert.IsFalse(string.IsNullOrWhiteSpace(inspection.Thumbprint));
        StringAssert.Contains(inspection.Subject, "localhost");
    }

    private static async Task<TcpPlaygroundServer> StartServerAsync(X509Certificate2 certificate)
    {
        var server = new TcpPlaygroundServer(new TcpPlaygroundServerOptions
        {
            ServerCertificate = certificate,
            AuthenticationSecret = AuthSecret,
            AdminUsername = AdminUser,
            AdminPassword = AdminPassword,
            DefaultCommandTimeout = TimeSpan.FromMilliseconds(500)
        });
        await server.StartAsync();
        return server;
    }

    private static TcpTestClient CreateLoginClient(int port, X509Certificate2 certificate, string deviceId, bool autoAuthenticate)
        => new(new TcpTestClientOptions
        {
            ClientId = deviceId,
            Role = DeviceRoles.Client,
            Port = port,
            RetryCount = 0,
            UseTls = true,
            AutoAuthenticate = autoAuthenticate,
            RemoteCertificateValidationCallback = MatchCertificate(certificate)
        });

    private static TcpTestClient CreateTokenClient(int port, X509Certificate2 certificate, string deviceId)
        => new(new TcpTestClientOptions
        {
            ClientId = deviceId,
            Role = DeviceRoles.Client,
            Port = port,
            RetryCount = 0,
            UseTls = true,
            AuthenticationSecret = AuthSecret,
            RemoteCertificateValidationCallback = MatchCertificate(certificate)
        });

    private static Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool> MatchCertificate(X509Certificate2 certificate)
        => (presentedCertificate, _, _) => presentedCertificate?.Thumbprint == certificate.Thumbprint;
}

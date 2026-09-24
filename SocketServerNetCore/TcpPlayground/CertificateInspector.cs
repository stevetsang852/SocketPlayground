using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public sealed record CertificateInspection(
    bool HasCertificate,
    string? Subject,
    string? Issuer,
    string? Thumbprint,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? NotAfterUtc,
    bool IsExpired);

public static class CertificateInspector
{
    public static CertificateInspection Inspect(X509Certificate? certificate)
    {
        if (certificate is null)
        {
            return new CertificateInspection(false, null, null, null, null, null, false);
        }

        using var cert = new X509Certificate2(certificate.GetRawCertData());
        var notBefore = new DateTimeOffset(cert.NotBefore.ToUniversalTime());
        var notAfter = new DateTimeOffset(cert.NotAfter.ToUniversalTime());
        return new CertificateInspection(
            true,
            cert.Subject,
            cert.Issuer,
            cert.Thumbprint,
            notBefore,
            notAfter,
            notAfter <= DateTimeOffset.UtcNow);
    }

    public static bool TryValidateServerCertificate(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors errors,
        bool allowUntrusted,
        out string? error)
    {
        error = null;
        var inspection = Inspect(certificate);
        if (!inspection.HasCertificate)
        {
            error = "Peer TLS certificate is missing.";
            return false;
        }

        if (inspection.IsExpired)
        {
            error = $"Peer TLS certificate '{inspection.Thumbprint}' has expired.";
            return false;
        }

        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        if (allowUntrusted && errors is SslPolicyErrors.RemoteCertificateChainErrors or SslPolicyErrors.RemoteCertificateNameMismatch or (SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return true;
        }

        error = $"Peer TLS certificate rejected: {errors}.";
        return false;
    }
}

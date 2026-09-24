using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SocketServerNetCore.TcpPlayground;

public static class DevelopmentCertificateLoader
{
    public static X509Certificate2 CreateLoopbackCertificate(string subjectName = "localhost")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subjectName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var enhancedKeyUsage = new OidCollection
        {
            new("1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, false));

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName(subjectName);
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(14));

        // EphemeralKeySet keeps the private key in-process on Linux so TLS tests
        // do not hit "m_safeCertContext is an invalid handle" after export.
        return new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    public static X509Certificate2 LoadFromFile(string certificatePath, string? password)
        => new(
            certificatePath,
            password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
}

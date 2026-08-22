using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HealthLens.Api.Services;

/// <summary>
/// Generates (and persists) a self-signed certificate so the container can serve HTTPS with zero setup
/// — needed only because Google's OAuth redirect URI must be https. The cert is self-signed with no
/// trusted CA behind it, so a browser shows a one-time warning the first time it's hit (inside the
/// OAuth popup only — the rest of the app keeps using plain http); there's no way around that warning
/// for an arbitrary local hostname without installing a local CA tool, which this app deliberately
/// avoids requiring.
/// </summary>
public static class LocalHttpsCertificate
{
    private const string Password = "healthlens-local-https";

    public static X509Certificate2 GetOrCreate(string pfxPath)
    {
        if (File.Exists(pfxPath))
        {
            try
            {
                return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, Password, X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException)
            {
                // Fall through and regenerate if the existing file is corrupt or unreadable.
            }
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false)); // Server Authentication

        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));

        Directory.CreateDirectory(Path.GetDirectoryName(pfxPath)!);
        File.WriteAllBytes(pfxPath, generated.Export(X509ContentType.Pfx, Password));

        return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, Password, X509KeyStorageFlags.Exportable);
    }
}

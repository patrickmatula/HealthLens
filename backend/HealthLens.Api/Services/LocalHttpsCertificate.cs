using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace HealthLens.Api.Services;

/// <summary>
/// Generates (and persists) a self-signed certificate so the container can serve HTTPS with zero setup
/// — needed only because Google's OAuth redirect URI must be https. The cert is self-signed with no
/// trusted CA behind it, so a browser shows a one-time warning the first time it's hit (inside the
/// OAuth popup only — the rest of the app keeps using plain http); there's no way around that warning
/// for an arbitrary local hostname without installing a local CA tool, which this app deliberately
/// avoids requiring.
///
/// The exported PKCS12 bytes (which embed the private key) are encrypted at rest with the same Data
/// Protection key ring the OAuth credential store uses, instead of a PKCS12 password -- a per-app-secret
/// password would still have to live somewhere readable by this process, and a fixed one is public
/// knowledge the moment the source is: anyone who obtains the .pfx (a stray backup, a misdirected volume
/// mount) wouldn't even need to find it, just look it up.
/// </summary>
public static class LocalHttpsCertificate
{
    public static X509Certificate2 GetOrCreate(string pfxPath, IDataProtector protector)
    {
        if (File.Exists(pfxPath))
        {
            try
            {
                var pfxBytes = protector.Unprotect(File.ReadAllBytes(pfxPath));
                return X509CertificateLoader.LoadPkcs12(pfxBytes, password: null, X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException)
            {
                // Fall through and regenerate if the existing file is corrupt, unreadable, or (from
                // before this fix) password-protected plaintext this protector can't unwrap.
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

        var rawPfxBytes = generated.Export(X509ContentType.Pfx);
        Directory.CreateDirectory(Path.GetDirectoryName(pfxPath)!);
        File.WriteAllBytes(pfxPath, protector.Protect(rawPfxBytes));

        return X509CertificateLoader.LoadPkcs12(rawPfxBytes, password: null, X509KeyStorageFlags.Exportable);
    }
}

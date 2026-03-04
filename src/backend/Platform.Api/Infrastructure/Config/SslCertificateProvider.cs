using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Platform.Api.Infrastructure.Config;

public static class SslCertificateProvider
{
    private static readonly string CertFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
        "PlatformaDapnet"
    );
    private static readonly string CertPath = Path.Combine(CertFolder, "localhost.pfx");
    private const string CertPassword = "Dapnet_Secure_SSL_2025";
    private const int RenewDaysBefore = 30;

    public static X509Certificate2 GetOrCreateCertificate()
    {
        if (!Directory.Exists(CertFolder)) Directory.CreateDirectory(CertFolder);
        if (File.Exists(CertPath))
        {
            try
            {
                var cert = new X509Certificate2(CertPath, CertPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                var now = DateTimeOffset.UtcNow;
                if (cert.NotAfter.ToUniversalTime() - now < TimeSpan.FromDays(RenewDaysBefore))
                {
                    // renew when close to expiry
                    return GenerateNewCertificate();
                }
                return cert;
            }
            catch { }
        }
        return GenerateNewCertificate();
    }

    private static X509Certificate2 GenerateNewCertificate()
    {
        using var rsa = RSA.Create(2048);
        // ✅ Używamy CN=localhost jako standardu
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        // ✅ KLUCZOWE: certificateAuthority = true (Ucisza błąd Basic Constraints)
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        
        // ✅ KLUCZOWE: KeyCertSign (Ucisza błąd keyCertSign bit)
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("127.0.0.1");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        // ✅ Używamy UtcNow bez milisekund dla stabilności podpisu
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero).AddDays(-1);
        var end = start.AddYears(1);

        var certificate = request.CreateSelfSigned(start, end);
        var pfxData = certificate.Export(X509ContentType.Pfx, CertPassword);
        File.WriteAllBytes(CertPath, pfxData);

        return new X509Certificate2(CertPath, CertPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
    }
}

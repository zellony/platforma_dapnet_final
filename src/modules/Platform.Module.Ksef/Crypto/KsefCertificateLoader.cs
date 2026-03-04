using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Platform.Api.Modules.KSeF.Crypto;

public static class KsefCertificateLoader
{
    private const string RsaOid = "1.2.840.113549.1.1.1";
    private const string EcOid = "1.2.840.10045.2.1";

    public static X509Certificate2 LoadFromCrtKey(byte[] crtBytes, byte[] keyBytes, string keyPassword)
    {
        ArgumentNullException.ThrowIfNull(crtBytes);
        ArgumentNullException.ThrowIfNull(keyBytes);

        var publicCert = LoadPublicCertificate(crtBytes);

        string privateKeyPem = ToUtf8String(keyBytes);
        return publicCert.MergeWithPemKey(privateKeyPem, keyPassword);
    }

    private static X509Certificate2 LoadPublicCertificate(byte[] certBytes)
    {
        // repo używa loadera bazującego na X509Certificate2 + flags
        // dodatkowo wspieramy PEM w .crt
        if (LooksLikePem(certBytes))
        {
            string pem = ToUtf8String(certBytes);
            return X509Certificate2.CreateFromPem(pem);
        }

        return new X509Certificate2(certBytes, string.Empty, GetFlags());
    }

    private static bool LooksLikePem(byte[] bytes)
    {
        // szybka detekcja "-----BEGIN"
        if (bytes.Length < 10) return false;
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 64));
        return head.Contains("-----BEGIN", StringComparison.Ordinal);
    }

    private static string ToUtf8String(byte[] bytes)
    {
        // PEM to tekst; dla DER to i tak nie używamy
        return Encoding.UTF8.GetString(bytes);
    }

    private static X509KeyStorageFlags GetFlags()
    {
        // macOS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return X509KeyStorageFlags.Exportable;

        // Windows / Linux
        return X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet;
    }

    // ==========================
    // 1:1 z repo: MergeWithPemKey
    // ==========================

    private static X509Certificate2 MergeWithPemKey(this X509Certificate2 publicCert, string privateKeyPem, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(publicCert);

        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new ArgumentNullException(nameof(privateKeyPem));

        if (publicCert.HasPrivateKey)
            throw new InvalidOperationException("Certyfikat zawiera już klucz prywatny.");

        string oid = publicCert.PublicKey.Oid?.Value ?? throw new NotSupportedException("Certyfikat nie zawiera klucza publicznego OID.");

        bool isEncrypted = privateKeyPem.Contains("ENCRYPTED PRIVATE KEY", StringComparison.OrdinalIgnoreCase);
        if (isEncrypted && string.IsNullOrEmpty(password))
            throw new ArgumentException("Zaszyfrowany klucz prywatny wymaga podania hasła.", nameof(password));

        if (oid == RsaOid)
        {
            using RSA rsa = RSA.Create();
            if (isEncrypted) rsa.ImportFromEncryptedPem(privateKeyPem, password);
            else rsa.ImportFromPem(privateKeyPem);

            return publicCert.CopyWithPrivateKey(rsa);
        }

        if (oid == EcOid)
        {
            using ECDsa ecdsa = ECDsa.Create();
            try
            {
                if (isEncrypted) ecdsa.ImportFromEncryptedPem(privateKeyPem, password);
                else ecdsa.ImportFromPem(privateKeyPem);
            }
            catch (CryptographicException ex) when (isEncrypted && IsPkcs8PasswordError(ex))
            {
                return publicCert.MergeWithPemKeyNoProfileForEcdsa(privateKeyPem, password!);
            }

            return publicCert.CopyWithPrivateKey(ecdsa);
        }

        throw new NotSupportedException($"Algorytym o OID '{oid}' nie jest wspierany.");
    }

    private static X509Certificate2 MergeWithPemKeyNoProfileForEcdsa(this X509Certificate2 publicCert, string privateKeyPem, string password)
    {
        ArgumentNullException.ThrowIfNull(publicCert);

        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new ArgumentNullException(nameof(privateKeyPem));

        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Szyfrowany klucz prywatny ECDSA wymaga podania hasła.", nameof(password));

        using ECDsa ecdsa = ECDsa.Create();

        byte[] encryptedPkcs8 = ExtractEncryptedPkcs8FromPem(privateKeyPem);

        int bytesRead;
        Pkcs8PrivateKeyInfo privateKeyInfo = Pkcs8PrivateKeyInfo.DecryptAndDecode(password.AsSpan(), encryptedPkcs8, out bytesRead);

        if (bytesRead != encryptedPkcs8.Length)
            throw new CryptographicException($"Nie udało się całkowicie zaimportować {nameof(Pkcs8PrivateKeyInfo)}. BytesRead={bytesRead}, Total={encryptedPkcs8.Length}.");

        if (!IsEcdsaPrivateKey(privateKeyInfo))
            throw new NotSupportedException($"Odszyfrowany klucz PKCS#8 nie jest poprawnym kluczem ECDSA. OID algorytmu: {privateKeyInfo.AlgorithmId.Value}.");

        ReadOnlyMemory<byte> pkcs8Der = privateKeyInfo.Encode();

        int importedBytes;
        ecdsa.ImportPkcs8PrivateKey(pkcs8Der.Span, out importedBytes);

        if (importedBytes != pkcs8Der.Length)
            throw new CryptographicException($"Nie udało się całkowicie zaimportować {nameof(Pkcs8PrivateKeyInfo)}. BytesRead={importedBytes}, Total={pkcs8Der.Length}.");

        return publicCert.CopyWithPrivateKey(ecdsa);
    }

    private static bool IsPkcs8PasswordError(CryptographicException cryptographyException)
    {
        string message = cryptographyException.Message ?? string.Empty;
        return message.Contains("EncryptedPrivateKeyInfo", StringComparison.OrdinalIgnoreCase)
               || message.Contains("password may be incorrect", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ExtractEncryptedPkcs8FromPem(string pem)
    {
        const string Begin = "-----BEGIN ENCRYPTED PRIVATE KEY-----";
        const string End = "-----END ENCRYPTED PRIVATE KEY-----";

        int begin = pem.IndexOf(Begin, StringComparison.OrdinalIgnoreCase);
        int end = pem.IndexOf(End, StringComparison.OrdinalIgnoreCase);

        if (begin < 0 || end < 0 || end <= begin)
            throw new CryptographicException("PEM nie zawiera poprawnego bloku ENCRYPTED PRIVATE KEY.");

        int base64Start = begin + Begin.Length;
        string base64 = pem.Substring(base64Start, end - base64Start);
        string normalized = new(base64.Where(c => !char.IsWhiteSpace(c)).ToArray());

        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException fe)
        {
            throw new CryptographicException("PEM ENCRYPTED PRIVATE KEY zawiera nieprawidłowe dane Base64.", fe);
        }
    }

    private static bool IsEcdsaPrivateKey(Pkcs8PrivateKeyInfo privateKeyInfo)
    {
        string algorithmOid = privateKeyInfo.AlgorithmId?.Value ?? string.Empty;
        return string.Equals(algorithmOid, EcOid, StringComparison.Ordinal);
    }
}

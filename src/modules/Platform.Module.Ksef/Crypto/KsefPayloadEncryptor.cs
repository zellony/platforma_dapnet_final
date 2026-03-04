using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Platform.Api.Modules.KSeF.Crypto;

/// <summary>
/// KSeF payload encryption:
/// - AES-256-CBC + PKCS7 padding (IV = 16 bytes)
/// - AES key encrypted using RSA-OAEP with SHA-256 (public key from MF cert usage=SymmetricKeyEncryption)
/// </summary>
public static class KsefPayloadEncryptor
{
    public sealed record Result(
        string EncryptedSymmetricKeyBase64,
        string InitializationVectorBase64,
        string EncryptedPayloadBase64,
        string Algorithm // pomocniczo do debug/trace
    );

    /// <param name="payload">Jawny payload (bytes) do zaszyfrowania AES</param>
    /// <param name="mfCertificateBase64Der">
    /// Certyfikat MF w formie Base64 DER (dokładnie to co przychodzi w polu "certificate" z /security/public-key-certificates)
    /// </param>
    public static Result Encrypt(byte[] payload, string mfCertificateBase64Der)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(mfCertificateBase64Der))
            throw new ArgumentException("Certyfikat MF (Base64 DER) jest wymagany.", nameof(mfCertificateBase64Der));

        // 1) Generate AES-256 key + IV(16)
        var aesKey = RandomNumberGenerator.GetBytes(32);  // 256-bit
        var iv = RandomNumberGenerator.GetBytes(16);  // 16 bytes

        // 2) Encrypt payload using AES-256-CBC/PKCS7
        byte[] encryptedPayload;
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            encryptedPayload = encryptor.TransformFinalBlock(payload, 0, payload.Length);
        }

        // 3) Encrypt AES key with MF public RSA using OAEP SHA-256
        byte[] encryptedSymmetricKey;
        var certBytes = Convert.FromBase64String(mfCertificateBase64Der);
        using (var cert = new X509Certificate2(certBytes))
        using (var rsa = cert.GetRSAPublicKey())
        {
            if (rsa is null)
                throw new InvalidOperationException("Certyfikat MF nie zawiera klucza RSA (GetRSAPublicKey() zwrócił null).");

            encryptedSymmetricKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        }

        return new Result(
            EncryptedSymmetricKeyBase64: Convert.ToBase64String(encryptedSymmetricKey),
            InitializationVectorBase64: Convert.ToBase64String(iv),
            EncryptedPayloadBase64: Convert.ToBase64String(encryptedPayload),
            Algorithm: "AES-256-CBC/PKCS7 + RSA-OAEP(SHA-256)"
        );
    }
}

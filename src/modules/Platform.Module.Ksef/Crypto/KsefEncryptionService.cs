using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;

namespace Platform.Api.Modules.KSeF.Crypto;

public sealed class KsefEncryptionService
{
    private const string UsageSymmetricKeyEncryption = "SymmetricKeyEncryption";

    private readonly IKsefHttpClient _http;

    public KsefEncryptionService(IKsefHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Produkcyjny klocek A2: buduje sekcję "encryption" + zaszyfrowany payload (Base64),
    /// korzystając z certyfikatu MF o usage=SymmetricKeyEncryption pobranego z KSeF.
    /// </summary>
    public async Task<KsefEncryptedRequest> EncryptPayloadAsync(
        Guid userId,
        KsefEnvironment env,
        string payloadUtf8,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payloadUtf8))
            throw new ArgumentException("Payload nie może być pusty.", nameof(payloadUtf8));

        // 1) Pobierz certyfikaty MF z KSeF (A1)
        var json = await _http.GetWithUserTokenAsync(userId, env, "security/public-key-certificates", ct);

        var certs = JsonSerializer.Deserialize<List<PublicKeyCertificateDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (certs is null || certs.Count == 0)
            throw new InvalidOperationException("KSeF zwrócił pustą listę certyfikatów MF.");

        // 2) Wybierz certyfikat usage=SymmetricKeyEncryption
        var cert = certs.FirstOrDefault(c =>
            c.Usage != null &&
            c.Usage.Any(u => string.Equals(u, UsageSymmetricKeyEncryption, StringComparison.OrdinalIgnoreCase)));

        if (cert is null || string.IsNullOrWhiteSpace(cert.Certificate))
            throw new InvalidOperationException($"Nie znaleziono certyfikatu MF z usage={UsageSymmetricKeyEncryption}.");

        // 3) Szyfruj payload (AES-256-CBC) + zaszyfruj klucz AES RSA-OAEP(SHA-256)
        var payloadBytes = Encoding.UTF8.GetBytes(payloadUtf8);
        var enc = KsefPayloadEncryptor.Encrypt(payloadBytes, cert.Certificate);

        // 4) Zwróć dokładnie format, który będzie częścią requestów KSeF
        return new KsefEncryptedRequest
        {
            Encryption = new KsefEncryptedRequest.EncryptionSection
            {
                EncryptedSymmetricKey = enc.EncryptedSymmetricKeyBase64,
                InitializationVector = enc.InitializationVectorBase64
            },
            EncryptedPayload = enc.EncryptedPayloadBase64
        };
    }

    private sealed class PublicKeyCertificateDto
    {
        public string? Certificate { get; set; }
        public List<string>? Usage { get; set; }
    }
}

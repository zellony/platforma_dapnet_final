using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Crypto;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/crypto")]
[Authorize]
public sealed class KsefCryptoController : ControllerBase
{
    private const string UsageSymmetricKeyEncryption = "SymmetricKeyEncryption";

    private readonly IKsefHttpClient _http;

    public KsefCryptoController(IKsefHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// A2 (debug): szyfruje payload zgodnie z wymaganiami KSeF:
    /// - AES-256-CBC/PKCS7
    /// - AES key zaszyfrowany RSA-OAEP(SHA-256) kluczem publicznym MF (usage=SymmetricKeyEncryption)
    /// </summary>
    [HttpPost("encrypt-payload")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> EncryptPayload(
        [FromQuery] KsefEnvironment env,
        [FromBody] EncryptPayloadRequest req,
        CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Payload))
            return BadRequest(new { error = "Payload jest wymagany (body: { payload: \"...\" })." });

        var userId = GetUserId();

        // Pobierz certyfikaty MF (A1) przez KSeF z tokenem usera (jak w kanonie)
        var json = await _http.GetWithUserTokenAsync(userId, env, "security/public-key-certificates", ct);

        var certs = JsonSerializer.Deserialize<List<PublicKeyCertificateDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (certs is null || certs.Count == 0)
            return StatusCode(502, new { error = "KSeF zwrócił pustą listę certyfikatów MF." });

        var cert = certs.FirstOrDefault(c =>
            c.Usage != null &&
            c.Usage.Any(u => string.Equals(u, UsageSymmetricKeyEncryption, StringComparison.OrdinalIgnoreCase)));

        if (cert is null || string.IsNullOrWhiteSpace(cert.Certificate))
            return StatusCode(502, new { error = $"Nie znaleziono certyfikatu MF z usage={UsageSymmetricKeyEncryption}." });

        // Szyfruj payload
        var payloadBytes = Encoding.UTF8.GetBytes(req.Payload);
        var enc = KsefPayloadEncryptor.Encrypt(payloadBytes, cert.Certificate);

        // Zwróć pola, które będą częścią requestu KSeF w A2/A3
        return Ok(new
        {
            selectedCertificate = new
            {
                usage = cert.Usage,
                validFrom = cert.ValidFrom,
                validTo = cert.ValidTo
            },
            encryption = new
            {
                encryptedSymmetricKey = enc.EncryptedSymmetricKeyBase64,
                initializationVector = enc.InitializationVectorBase64
            },
            encryptedPayload = enc.EncryptedPayloadBase64,
            algorithm = enc.Algorithm
        });
    }

    public sealed class EncryptPayloadRequest
    {
        public string Payload { get; set; } = string.Empty;
    }

    private sealed class PublicKeyCertificateDto
    {
        public string? Certificate { get; set; }
        public DateTimeOffset? ValidFrom { get; set; }
        public DateTimeOffset? ValidTo { get; set; }
        public List<string>? Usage { get; set; }
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
            throw new InvalidOperationException("Brak claim 'sub' w JWT.");
        return Guid.Parse(sub);
    }
}

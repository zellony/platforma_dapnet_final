using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Services;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/credentials")]
[Authorize]
public sealed class KsefCredentialsController : ControllerBase
{
    private readonly IKsefCredentialService _cred;

    public KsefCredentialsController(IKsefCredentialService cred)
    {
        _cred = cred;
    }

    [HttpGet]
    [RequirePermission("ksef.view")]
    public async Task<ActionResult<KsefCredentialStatusResponse>> GetStatus(
        [FromQuery] KsefEnvironment env,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var row = await _cred.GetAsync(userId, env, ct);

        if (row is null)
            return Ok(new KsefCredentialStatusResponse(env, "NONE", null, null));

        return Ok(new KsefCredentialStatusResponse(
            env,
            row.ActiveType.ToString(),
            row.CertFingerprintSha256,
            row.TokenValidTo
        ));
    }

    [HttpPost("certificate")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> SetCertificate(
        [FromBody] SetCertificateRequest req,
        CancellationToken ct)
    {
        var userId = GetUserId();

        byte[] crt = Convert.FromBase64String(req.CertCrtBase64);
        byte[] key = Convert.FromBase64String(req.CertKeyBase64);

        await _cred.UpsertCertificateAsync(
            userId,
            req.Environment,
            crt,
            key,
            req.CertKeyPassword,
            ct);

        return NoContent();
    }

    [HttpPost("token")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> SetToken(
        [FromBody] SetTokenRequest req,
        CancellationToken ct)
    {
        var userId = GetUserId();

        await _cred.UpsertTokenAsync(
            userId,
            req.Environment,
            req.Token,
            req.TokenValidTo,
            ct);

        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
            throw new InvalidOperationException("Brak claim 'sub' w JWT.");

        return Guid.Parse(sub);
    }
}

public sealed record SetCertificateRequest(
    KsefEnvironment Environment,
    string CertCrtBase64,
    string CertKeyBase64,
    string CertKeyPassword
);

public sealed record SetTokenRequest(
    KsefEnvironment Environment,
    string Token,
    DateTimeOffset? TokenValidTo
);

public sealed record KsefCredentialStatusResponse(
    KsefEnvironment Environment,
    string ActiveType,                 // NONE/CERT/TOKEN
    string? CertFingerprintSha256,
    DateTimeOffset? TokenValidTo
);

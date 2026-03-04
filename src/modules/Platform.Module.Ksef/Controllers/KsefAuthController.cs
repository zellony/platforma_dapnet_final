using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Auth;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/auth")]
[Authorize]
public sealed class KsefAuthController : ControllerBase
{
    private readonly IKsefHttpClient _http;
    private readonly KsefAuthService _auth;

    public KsefAuthController(IKsefHttpClient http, KsefAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    [HttpPost("login/certificate")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> LoginWithCertificate([FromQuery] KsefEnvironment env, CancellationToken ct)
    {
        var userId = GetUserId();
        var initJson = await _auth.StartLoginWithCertificateAsync(userId, env, ct);
        return Content(initJson, "application/json");
    }

    [HttpGet("status")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> GetStatus(
        [FromQuery] KsefEnvironment env,
        [FromQuery] string referenceNumber,
        [FromQuery] string authenticationToken,
        CancellationToken ct)
    {
        var json = await _http.GetAsync(env, $"auth/{referenceNumber}", bearerToken: authenticationToken, ct);
        return Content(json, "application/json");
    }

    [HttpPost("redeem")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> RedeemToken(
        [FromQuery] KsefEnvironment env,
        [FromQuery] string authenticationToken,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var json = await _http.PostAsync(env, "auth/token/redeem", bearerToken: authenticationToken, body: null, contentType: "application/json", ct);

        await _auth.SaveTokensFromRedeemAsync(userId, env, json, ct);

        return Content(json, "application/json");
    }

    // TERAZ sessions używa auto-refresh z KsefHttpClient
    [HttpGet("sessions")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> GetSessions([FromQuery] KsefEnvironment env, CancellationToken ct)
    {
        var userId = GetUserId();
        var json = await _http.GetWithUserTokenAsync(userId, env, "auth/sessions", ct);
        return Content(json, "application/json");
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
            throw new InvalidOperationException("Brak claim 'sub' w JWT.");
        return Guid.Parse(sub);
    }
}

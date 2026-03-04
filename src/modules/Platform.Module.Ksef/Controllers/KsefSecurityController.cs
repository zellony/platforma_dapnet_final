using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/security")]
[Authorize]
public sealed class KsefSecurityController : ControllerBase
{
    private readonly IKsefHttpClient _http;

    public KsefSecurityController(IKsefHttpClient http)
    {
        _http = http;
    }

    [HttpGet("public-key-certificates")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> GetPublicKeyCertificates(
        [FromQuery] KsefEnvironment env,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var json = await _http.GetWithUserTokenAsync(userId, env, "security/public-key-certificates", ct);
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

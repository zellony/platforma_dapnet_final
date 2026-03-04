using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Core.Licensing;
using Platform.Kernel.Core.RBAC;

namespace Platform.Api.Controllers.System;

[ApiController]
[Route("license")]
public class LicenseController : ControllerBase
{
    private readonly LicenseService _licenseService;

    public LicenseController(LicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    [HttpGet("status")]
    [AllowAnonymous] // ✅ DOSTĘPNE DLA KAŻDEGO
    public async Task<IActionResult> GetStatus()
    {
        var status = await _licenseService.GetCurrentStatusAsync();
        return Ok(status);
    }

    [HttpPost("upload")]
    [AllowAnonymous] // ✅ WARUNKOWY DOSTĘP
    public async Task<IActionResult> UploadLicense([FromBody] UploadLicenseDto req)
    {
        var currentStatus = await _licenseService.GetCurrentStatusAsync();

        // Jeśli licencja już jest aktywna, wymagamy uprawnień admina do jej zmiany
        if (currentStatus.IsActive && !User.Identity!.IsAuthenticated)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(req.LicenseContent))
            return BadRequest("Brak treści licencji.");

        var result = await _licenseService.VerifyAndSaveAsync(req.LicenseContent);

        return result switch
        {
            LicenseVerificationResult.Valid => Ok(new { message = "Licencja została pomyślnie aktywowana." }),
            LicenseVerificationResult.Expired => BadRequest("Licencja wygasła."),
            LicenseVerificationResult.NipMismatch => BadRequest("Licencja jest wystawiona na inny NIP."),
            LicenseVerificationResult.CompanyNotConfigured => BadRequest("Najpierw skonfiguruj dane firmy."),
            _ => BadRequest("Plik licencji jest nieprawidłowy lub uszkodzony.")
        };
    }

    public record UploadLicenseDto(string LicenseContent);
}

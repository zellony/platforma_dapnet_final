using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database;
using Platform.Kernel.Core.Config.Entities;
using Platform.Kernel.Core.RBAC;

namespace Platform.Api.Controllers.System;

[ApiController]
[Route("company")]
public class CompanyController : ControllerBase
{
    private readonly AppDbContext _db;

    public CompanyController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus()
    {
        var isConfigured = await _db.CompanyInfos.AnyAsync(c => !string.IsNullOrWhiteSpace(c.Nip));
        return Ok(new { isConfigured });
    }

    // ✅ NOWY ENDPOINT: Publiczne dane firmy (potrzebne do aktywacji licencji)
    [HttpGet("public-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicInfo()
    {
        var info = await _db.CompanyInfos
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new { c.Nip, c.FullName })
            .FirstOrDefaultAsync();
        if (info == null) return NotFound();
        return Ok(info);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<CompanyInfo>> Get()
    {
        var info = await _db.CompanyInfos
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();
        if (info == null) return NotFound();
        return Ok(info);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Save([FromBody] CompanyInfo req)
    {
        var existing = await _db.CompanyInfos
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();
        
        if (existing != null && !User.Identity!.IsAuthenticated)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(req.Nip) || string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest("NIP i Pełna nazwa są wymagane.");

        if (existing == null)
        {
            req.Id = Guid.NewGuid();
            _db.CompanyInfos.Add(req);
        }
        else
        {
            existing.FullName = req.FullName;
            existing.ShortName = req.ShortName;
            existing.Nip = req.Nip;
            existing.Address = req.Address;
            existing.City = req.City;
            existing.PostalCode = req.PostalCode;
            existing.Email = req.Email;
            existing.PhoneNumber = req.PhoneNumber;
        }

        await _db.SaveChangesAsync();
        return Ok(new { isConfigured = true });
    }
}

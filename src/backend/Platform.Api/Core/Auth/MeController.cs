using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database;
using System.Security.Claims;

namespace Platform.Api.Core.Auth;

[ApiController]
[Route("auth")]
public sealed class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MeController> _logger;

    public MeController(AppDbContext db, ILogger<MeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try 
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isSystemAdmin = User.HasClaim("is_system_admin", "true");
            
            // Read token expiry from current JWT token.
            var expClaim = User.FindFirst("exp")?.Value;
            long? expiresAt = expClaim != null ? long.Parse(expClaim) : null;

            List<string> permissions = new();
            
            if (isSystemAdmin)
            {
                permissions = await _db.Permissions
                    .AsNoTracking()
                    .Select(p => p.Code)
                    .ToListAsync();

                if (permissions.Count == 0)
                {
                    permissions.AddRange(new[] { 
                        "platform.admin", 
                        "platform.users.read", 
                        "platform.users.write",
                        "ksef.view"
                    });
                }
            }
            else
            {
                if (string.IsNullOrEmpty(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
                    return Unauthorized();

                permissions = await _db.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.UserId == userId)
                    .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
                    .Join(_db.Permissions, pid => pid, p => p.Id, (pid, p) => p.Code)
                    .Distinct()
                    .ToListAsync();
            }

            return Ok(new
            {
                userId = userIdValue,
                login = User.FindFirstValue(ClaimTypes.Name),
                isSystemAdmin = isSystemAdmin,
                permissions = permissions,
                expiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthMe] Failed to resolve current user profile.");
            return StatusCode(500, "Internal server error");
        }
    }
}

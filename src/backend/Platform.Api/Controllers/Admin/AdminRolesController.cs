using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Platform.Kernel.Core.RBAC;
using Platform.Kernel.Core.RBAC.Entities;
using Platform.Api.Infrastructure.Database;

namespace Platform.Api.Controllers.Admin;

[ApiController]
[Route("admin/roles")]
[Authorize]
public sealed class AdminRolesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminRolesController(AppDbContext db)
    {
        _db = db;
    }

    public sealed record RoleDto(Guid Id, string Name);
    public sealed record CreateRoleRequest(string Name);
    public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> Permissions);

    /// <summary>
    /// Lista ról.
    /// </summary>
    [HttpGet]
    // ✅ ZMIANA: Pozwalamy na odczyt ról użytkownikom z uprawnieniem platform.users.read
    [RequirePermission("platform.users.read")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles()
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name))
            .ToListAsync();

        return Ok(roles);
    }

    /// <summary>
    /// Dodanie roli.
    /// </summary>
    [HttpPost]
    [RequirePermission("platform.admin")]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest req)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return ValidationProblem("Name is required");

        var exists = await _db.Roles.AnyAsync(r => r.Name.ToLower() == name.ToLower());
        if (exists)
            return Conflict(new ProblemDetails
            {
                Title = "Role already exists",
                Detail = $"Role '{name}' already exists."
            });

        var role = new Role { Name = name };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return Created($"/admin/roles/{Uri.EscapeDataString(role.Name)}", new RoleDto(role.Id, role.Name));
    }

    /// <summary>
    /// Uprawnienia dla roli.
    /// roleKey = nazwa roli (np. Admin, User)
    /// </summary>
    [HttpGet("{roleKey}/permissions")]
    // ✅ ZMIANA: Pozwalamy na podgląd uprawnień roli użytkownikom z uprawnieniem platform.users.read
    [RequirePermission("platform.users.read")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRolePermissions([FromRoute] string roleKey)
    {
        var role = await FindRoleByKey(roleKey);
        if (role is null) return NotFound();

        var codes = await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Join(_db.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => p.Code)
            .OrderBy(x => x)
            .ToListAsync();

        return Ok(codes);
    }

    /// <summary>
    /// Zapis uprawnień roli (nadpisuje zestaw uprawnień).
    /// </summary>
    [HttpPut("{roleKey}/permissions")]
    [RequirePermission("platform.admin")]
    public async Task<IActionResult> UpdateRolePermissions(
        [FromRoute] string roleKey,
        [FromBody] UpdateRolePermissionsRequest req)
    {
        var role = await FindRoleByKey(roleKey);
        if (role is null) return NotFound();

        var requestedCodes = (req.Permissions ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dbPermissions = await _db.Permissions
            .Where(p => requestedCodes.Contains(p.Code))
            .Select(p => new { p.Id, p.Code })
            .ToListAsync();

        var unknown = requestedCodes
            .Where(c => !dbPermissions.Any(p => string.Equals(p.Code, c, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (unknown.Count > 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unknown permissions",
                Detail = "One or more permission codes do not exist.",
                Extensions = { ["unknown"] = unknown }
            });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var existing = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync();

        if (existing.Count > 0)
            _db.RolePermissions.RemoveRange(existing);

        if (dbPermissions.Count > 0)
        {
            var toAdd = dbPermissions.Select(p => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = p.Id
            });

            _db.RolePermissions.AddRange(toAdd);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }

    /// <summary>
    /// Usunięcie roli.
    /// Bezpiecznie: blokuje usunięcie, jeśli rola jest przypisana do użytkowników.
    /// Czyści role_permissions i usuwa rekord roles.
    /// </summary>
    [HttpDelete("{roleKey}")]
    [RequirePermission("platform.admin")]
    public async Task<IActionResult> DeleteRole([FromRoute] string roleKey)
    {
        var role = await FindRoleByKey(roleKey);
        if (role is null) return NotFound();

        var usersCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == role.Id);
        if (usersCount > 0)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Role is in use",
                Detail = $"Role '{role.Name}' is assigned to {usersCount} user(s) and cannot be deleted."
            });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var rolePerms = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync();

        if (rolePerms.Count > 0)
            _db.RolePermissions.RemoveRange(rolePerms);

        _db.Roles.Remove(role);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }

    private async Task<Role?> FindRoleByKey(string roleKey)
    {
        var key = (roleKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key)) return null;

        // roleKey w URL traktujemy jako Role.Name (unikalne)
        return await _db.Roles.SingleOrDefaultAsync(r => r.Name.ToLower() == key.ToLower());
    }
}

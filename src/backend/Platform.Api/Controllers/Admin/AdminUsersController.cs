using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Platform.Kernel.Core.RBAC;
using Platform.Kernel.Core.RBAC.Entities;
using Platform.Api.Infrastructure.Database;

namespace Platform.Api.Controllers.Admin;

[ApiController]
[Route("admin/users")]
[Authorize]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AdminUsersController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public sealed record UserDto(Guid Id, string Login, bool IsActive, string? ExternalUserId, string? AdUpn, DateTime CreatedAtUtc, DateTime? LastActivityAtUtc, IReadOnlyList<string> Roles, bool IsOnline);
    public sealed record CreateUserRequest(string Login, string Password, bool? IsActive, string? ExternalUserId, string? AdUpn, IReadOnlyList<string>? Roles);
    public sealed record UpdateUserRequest(string? Login, string? Password, bool? IsActive, string? ExternalUserId, string? AdUpn, IReadOnlyList<string>? Roles);
    public sealed record UserSessionDto(Guid Id, DateTime LoginAtUtc, DateTime? LogoutAtUtc, string? LogoutReason, string? IpAddress, string? MachineName, string? UserAgent, bool IsActive);

    [HttpGet]
    [RequirePermission("platform.users.read")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers()
    {
        Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.Login).Select(u => new { u.Id, u.Login, u.IsActive, u.ExternalUserId, u.AdUpn, u.CreatedAtUtc, u.LastActivityAtUtc }).ToListAsync();
        if (users.Count == 0) return Ok(Array.Empty<UserDto>());
        var userIds = users.Select(u => u.Id).ToList();
        var activeUserIds = await _db.UserSessions.AsNoTracking().Where(s => userIds.Contains(s.UserId) && s.LogoutAtUtc == null).Select(s => s.UserId).Distinct().ToListAsync();
        var rolePairs = await _db.UserRoles.AsNoTracking().Where(ur => userIds.Contains(ur.UserId)).Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name }).ToListAsync();
        var rolesByUser = rolePairs.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList());
        var dto = users.Select(u => { rolesByUser.TryGetValue(u.Id, out var roles); roles ??= Array.Empty<string>(); bool isOnline = activeUserIds.Contains(u.Id); return new UserDto(u.Id, u.Login, u.IsActive, u.ExternalUserId, u.AdUpn, u.CreatedAtUtc, u.LastActivityAtUtc, roles, isOnline); }).ToList();
        return Ok(dto);
    }

    [HttpGet("{id:guid}/sessions")]
    [RequirePermission("platform.users.read")]
    public async Task<ActionResult<IReadOnlyList<UserSessionDto>>> GetUserSessions([FromRoute] Guid id)
    {
        var sessions = await _db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == id)
            .OrderByDescending(s => s.LoginAtUtc)
            .Take(50) // Ostatnie 50 sesji
            .Select(s => new UserSessionDto(
                s.Id, 
                s.LoginAtUtc, 
                s.LogoutAtUtc, 
                s.LogoutReason, 
                s.IpAddress, 
                s.MachineName, 
                s.UserAgent,
                s.LogoutAtUtc == null
            ))
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpPost("sessions/{sessionId:guid}/kill")]
    [RequirePermission("platform.users.write")]
    public async Task<IActionResult> KillSession([FromRoute] Guid sessionId)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return NotFound();
        if (session.LogoutAtUtc != null) return BadRequest("Sesja jest juĹĽ zakoĹ„czona.");

        session.LogoutAtUtc = DateTime.UtcNow;
        session.LogoutReason = "Killed by Admin";
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("setup-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSetupStatus()
    {
        var anyClientAdmin = await _db.Users.AnyAsync();
        return Ok(new { adminRequired = !anyClientAdmin });
    }

    [HttpPost("create-first-admin")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateFirstAdmin([FromBody] CreateUserRequest req)
    {
        var anyClientAdmin = await _db.Users.AnyAsync();
        if (anyClientAdmin) return BadRequest("Administrator klienta juĹĽ istnieje.");

        var login = (req.Login ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(login)) return BadRequest("Login jest wymagany.");
        
        string serviceLogin = (_config["ServiceAccount:Login"] ?? "AdminDAPNET").Trim();
        if (login.ToLower() == serviceLogin.ToLower()) return BadRequest("Ten login jest zarezerwowany.");

        var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole == null) return BadRequest("Rola 'Admin' nie istnieje.");

        var user = new User { Login = login, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password), IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpPost]
    [RequirePermission("platform.users.write")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest req)
    {
        var login = (req.Login ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(login)) return BadRequest("Login jest wymagany.");
        if (await _db.Users.AnyAsync(u => u.Login.ToLower() == login.ToLower())) return Conflict("UĹĽytkownik o takim loginie juĹĽ istnieje.");
        var requestedRoles = NormalizeRoleNames(req.Roles);
        var roles = await LoadRolesByNames(requestedRoles);
        await using var tx = await _db.Database.BeginTransactionAsync();
        var user = new User { Login = login, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password), IsActive = req.IsActive ?? true, ExternalUserId = req.ExternalUserId, AdUpn = req.AdUpn, CreatedAtUtc = DateTime.UtcNow };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        if (roles.Count > 0) { _db.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id })); await _db.SaveChangesAsync(); }
        await tx.CommitAsync();
        return Created($"/admin/users/{user.Id}", await GetUserDto(user.Id));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("platform.users.write")]
    public async Task<ActionResult<UserDto>> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        
        if (req.Login is not null) user.Login = req.Login.Trim();
        if (req.Password is not null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        if (req.IsActive is not null) user.IsActive = req.IsActive.Value;
        
        user.ExternalUserId = req.ExternalUserId;
        user.AdUpn = req.AdUpn;
        await _db.SaveChangesAsync();
        
        if (req.Roles is not null) {
            var roles = await LoadRolesByNames(NormalizeRoleNames(req.Roles));
            var existing = await _db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
            _db.UserRoles.RemoveRange(existing);
            _db.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id }));
            await _db.SaveChangesAsync();
        }
        return Ok(await GetUserDto(user.Id));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("platform.users.write")]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        try {
            var sessions = await _db.UserSessions.Where(s => s.UserId == id).ToListAsync();
            if (sessions.Any()) _db.UserSessions.RemoveRange(sessions);
            var roles = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
            if (roles.Any()) _db.UserRoles.RemoveRange(roles);
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return NoContent();
        } catch (Exception ex) {
            return BadRequest($"BĹ‚Ä…d bazy danych: {ex.Message}");
        }
    }

    private async Task<UserDto> GetUserDto(Guid id) {
        var u = await _db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var roles = await _db.UserRoles.AsNoTracking().Where(ur => ur.UserId == id).Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).ToListAsync();
        bool isOnline = await _db.UserSessions.AsNoTracking().AnyAsync(s => s.UserId == id && s.LogoutAtUtc == null);
        return new UserDto(u.Id, u.Login, u.IsActive, u.ExternalUserId, u.AdUpn, u.CreatedAtUtc, u.LastActivityAtUtc, roles, isOnline);
    }
    private static List<string> NormalizeRoleNames(IReadOnlyList<string>? roles) => (roles ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private async Task<List<Role>> LoadRolesByNames(IReadOnlyList<string> roleNames) { var lowered = roleNames.Select(r => r.ToLower()).ToList(); return await _db.Roles.Where(r => lowered.Contains(r.Name.ToLower())).ToListAsync(); }
}


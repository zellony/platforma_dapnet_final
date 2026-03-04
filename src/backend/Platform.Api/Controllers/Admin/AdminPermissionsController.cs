using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Platform.Kernel.Core.RBAC;
using Platform.Api.Infrastructure.Database;

namespace Platform.Api.Controllers.Admin;

[ApiController]
[Route("admin/permissions")]
[Authorize]
public sealed class AdminPermissionsController : ControllerBase
{
	private readonly AppDbContext _db;

	public AdminPermissionsController(AppDbContext db)
	{
		_db = db;
	}

	public sealed record PermissionDto(Guid Id, string Code, string? Description, string? ModuleName);

	/// <summary>
	/// Lista wszystkich uprawnień.
	/// </summary>
	[HttpGet]
	// ✅ ZMIANA: Pozwalamy użytkownikom z prawem do odczytu użytkowników widzieć listę uprawnień (potrzebne do filtrów)
	[RequirePermission("platform.users.read")]
	public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetAll()
	{
		var list = await _db.Permissions
			.AsNoTracking()
			.OrderBy(p => p.Code)
			.Select(p => new PermissionDto(p.Id, p.Code, p.Description, p.ModuleName))
			.ToListAsync();

		return Ok(list);
	}
}

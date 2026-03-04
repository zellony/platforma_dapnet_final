using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database;
using Platform.Kernel.Core.RBAC;
using System.Security.Claims;

namespace Platform.Api.Core.RBAC;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _db;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(AppDbContext db, ILogger<PermissionAuthorizationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // SuperAdmin bypass
        var isSystemAdmin = context.User.HasClaim("is_system_admin", "true");
        if (isSystemAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogDebug("[RBAC] Checking permission {Permission} for UserID={UserIdValue}", requirement.Permission, userIdValue);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            _logger.LogWarning("[RBAC] Failed to parse user id claim as Guid. UserIdValue={UserIdValue}", userIdValue);
            return;
        }

        var hasPermission = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(
                _db.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId
            )
            .Join(
                _db.Permissions,
                pid => pid,
                p => p.Id,
                (pid, p) => p.Code
            )
            .AnyAsync(code => code == requirement.Permission);

        if (hasPermission)
        {
            _logger.LogDebug("[RBAC] Permission granted. UserId={UserId}, Permission={Permission}", userId, requirement.Permission);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogInformation("[RBAC] Permission denied. UserId={UserId}, Permission={Permission}", userId, requirement.Permission);
        }
    }
}

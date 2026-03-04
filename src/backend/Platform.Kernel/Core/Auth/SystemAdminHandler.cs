using Microsoft.AspNetCore.Authorization;

namespace Platform.Kernel.Core.Auth;

public sealed class SystemAdminHandler : AuthorizationHandler<SystemAdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SystemAdminRequirement requirement)
    {
        if (context.User.HasClaim("is_system_admin", "true"))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

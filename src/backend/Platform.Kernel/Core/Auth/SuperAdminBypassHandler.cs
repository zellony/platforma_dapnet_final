using Microsoft.AspNetCore.Authorization;

namespace Platform.Kernel.Core.Auth;

// Jeśli token ma is_system_admin=true, to zaliczamy WSZYSTKIE wymagania autoryzacyjne
public sealed class SuperAdminBypassHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.User.HasClaim("is_system_admin", "true"))
        {
            foreach (var req in context.PendingRequirements.ToArray())
                context.Succeed(req);
        }

        return Task.CompletedTask;
    }
}

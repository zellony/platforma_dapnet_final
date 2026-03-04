using Microsoft.AspNetCore.Authorization;

namespace Platform.Kernel.Core.RBAC;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = $"PERMISSION:{permission}";
    }
}

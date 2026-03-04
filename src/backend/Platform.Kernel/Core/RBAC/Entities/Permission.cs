namespace Platform.Kernel.Core.RBAC.Entities;

public sealed class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public string? ModuleName { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();
}

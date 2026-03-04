namespace Platform.Kernel.Core.RBAC.Entities;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();
}

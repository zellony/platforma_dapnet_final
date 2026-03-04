using System;

namespace Platform.Kernel.Core.RBAC.Entities
{
    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = default!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = default!;
    }
}

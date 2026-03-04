namespace Platform.Kernel.Core.RBAC.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Login { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public string? ExternalUserId { get; set; }
    public string? AdUpn { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    // NOWE POLE: Śledzenie aktywności w czasie rzeczywistym
    public DateTime? LastActivityAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

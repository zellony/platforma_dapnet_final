namespace Platform.Kernel.Core.RBAC.Entities;

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime LoginAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LogoutAtUtc { get; set; }
    
    public string? LogoutReason { get; set; } 

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? InstanceId { get; set; }
    
    // NOWE POLE: Nazwa komputera klienta
    public string? MachineName { get; set; }
}

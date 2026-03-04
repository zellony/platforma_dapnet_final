using System;

namespace Platform.Kernel.Core.Config.Entities;

public class SystemLicense
{
    public Guid Id { get; set; }
    public string LicenseBlob { get; set; } = string.Empty; // Treść pliku .lic
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public bool IsActive { get; set; }
}

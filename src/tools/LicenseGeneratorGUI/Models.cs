using System.ComponentModel.DataAnnotations;

namespace LicenseGeneratorGUI.Models;

public class AppSettings
{
    [Key]
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ModuleDef
{
    [Key]
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Company
{
    public Guid Id { get; set; }
    [Required]
    public string Nip { get; set; } = "";
    [Required]
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<IssuedLicense> Licenses { get; set; } = new();
}

public class IssuedLicense
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UpdateUntil { get; set; }
    public int Seats { get; set; }
    
    public string ModulesJson { get; set; } = "[]"; 
    public string LicenseBlob { get; set; } = "";
}

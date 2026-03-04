using System.ComponentModel.DataAnnotations;

namespace Platform.Kernel.Core.Config.Entities;

public class SystemConfig
{
    [Key]
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

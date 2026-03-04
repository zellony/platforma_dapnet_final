using Microsoft.EntityFrameworkCore;
using LicenseGeneratorGUI.Models;

namespace LicenseGeneratorGUI;

public class GeneratorDbContext : DbContext
{
    public DbSet<AppSettings> Settings { get; set; }
    public DbSet<ModuleDef> Modules { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<IssuedLicense> Licenses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // ✅ SZYFROWANIE BAZY HASŁEM
        options.UseSqlite("Data Source=generator.db;Password=GreenIsTheBest");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().HasIndex(c => c.Nip).IsUnique();
        modelBuilder.Entity<IssuedLicense>().HasOne(l => l.Company).WithMany(c => c.Licenses).HasForeignKey(l => l.CompanyId);
    }
}

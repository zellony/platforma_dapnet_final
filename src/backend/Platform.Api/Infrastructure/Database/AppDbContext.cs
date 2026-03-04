using System;
using Microsoft.EntityFrameworkCore;
using Platform.Kernel.Core.RBAC.Entities;
using Platform.Kernel.Core.Config.Entities;

namespace Platform.Api.Infrastructure.Database;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<CompanyInfo> CompanyInfos => Set<CompanyInfo>();
    public DbSet<SystemLicense> SystemLicenses => Set<SystemLicense>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app_core");

        modelBuilder.Entity<SystemConfig>(e =>
        {
            e.ToTable("system_configs");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(100);
            e.Property(x => x.Value).HasColumnName("value").IsRequired();
            e.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });

        modelBuilder.Entity<CompanyInfo>(e =>
        {
            e.ToTable("company_info");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FullName).HasColumnName("full_name").IsRequired().HasMaxLength(255);
            e.Property(x => x.ShortName).HasColumnName("short_name").HasMaxLength(100);
            e.Property(x => x.Nip).HasColumnName("nip").IsRequired().HasMaxLength(20);
            e.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
            e.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            e.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(20);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50);
        });

        modelBuilder.Entity<SystemLicense>(e =>
        {
            e.ToTable("system_license");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LicenseBlob).HasColumnName("license_blob").IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(x => x.LastVerifiedAtUtc).HasColumnName("last_verified_at_utc");
            e.Property(x => x.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Login).HasColumnName("login").IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired().HasMaxLength(200);
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.ExternalUserId).HasColumnName("external_user_id");
            e.Property(x => x.AdUpn).HasColumnName("ad_upn");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(x => x.LastActivityAtUtc).HasColumnName("last_activity_at_utc");
        });

        modelBuilder.Entity<UserSession>(e =>
        {
            e.ToTable("user_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.LoginAtUtc).HasColumnName("login_at_utc");
            e.Property(x => x.LogoutAtUtc).HasColumnName("logout_at_utc");
            e.Property(x => x.LogoutReason).HasColumnName("logout_reason").HasMaxLength(50);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.InstanceId).HasColumnName("instance_id").HasMaxLength(64);
            e.Property(x => x.MachineName).HasColumnName("machine_name").HasMaxLength(100);
            e.HasIndex(x => x.InstanceId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Code).HasColumnName("code").IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.ModuleName).HasColumnName("module_name");
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.Property(x => x.PermissionId).HasColumnName("permission_id");
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            var name = asm.GetName().Name ?? string.Empty;
            if (!name.StartsWith("Platform.Module.", StringComparison.OrdinalIgnoreCase)) continue;
            modelBuilder.ApplyConfigurationsFromAssembly(asm);
        }
    }

    public void EnsureConfigTablesExist()
    {
        // NAJPIERW SCHEMAT
        Database.ExecuteSqlRaw("CREATE SCHEMA IF NOT EXISTS app_core;");

        var sqlConfig = @"
            CREATE TABLE IF NOT EXISTS app_core.system_configs (
                key character varying(100) NOT NULL,
                value text NOT NULL,
                updated_at_utc timestamp with time zone NOT NULL,
                CONSTRAINT pk_system_configs PRIMARY KEY (key)
            );
        ";
        Database.ExecuteSqlRaw(sqlConfig);

        var sqlCompany = @"
            CREATE TABLE IF NOT EXISTS app_core.company_info (
                id uuid NOT NULL,
                full_name character varying(255) NOT NULL,
                short_name character varying(100),
                nip character varying(20) NOT NULL,
                address character varying(255),
                city character varying(100),
                postal_code character varying(20),
                email character varying(100),
                phone_number character varying(50),
                CONSTRAINT pk_company_info PRIMARY KEY (id)
            );
        ";
        Database.ExecuteSqlRaw(sqlCompany);

        var sqlLicense = @"
            CREATE TABLE IF NOT EXISTS app_core.system_license (
                id uuid NOT NULL,
                license_blob text NOT NULL,
                created_at_utc timestamp with time zone NOT NULL,
                last_verified_at_utc timestamp with time zone,
                is_active boolean NOT NULL DEFAULT false,
                CONSTRAINT pk_system_license PRIMARY KEY (id)
            );
        ";
        Database.ExecuteSqlRaw(sqlLicense);
    }
}

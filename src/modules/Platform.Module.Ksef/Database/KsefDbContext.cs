using Microsoft.EntityFrameworkCore;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefDbContext : DbContext
{
    public KsefDbContext(DbContextOptions<KsefDbContext> options) : base(options) { }

    public DbSet<KsefUserCredential> UserCredentials => Set<KsefUserCredential>();
    public DbSet<KsefSyncState> SyncStates => Set<KsefSyncState>();
    public DbSet<KsefSyncLock> SyncLocks => Set<KsefSyncLock>();
    public DbSet<KsefRateLimitState> RateLimitStates => Set<KsefRateLimitState>();
    public DbSet<KsefPurchaseInvoice> PurchaseInvoices => Set<KsefPurchaseInvoice>();
    public DbSet<KsefAutoSyncSettings> AutoSyncSettings => Set<KsefAutoSyncSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new KsefUserCredentialConfig());
        modelBuilder.ApplyConfiguration(new KsefSyncStateConfig());
        modelBuilder.ApplyConfiguration(new KsefSyncLockConfig());
        modelBuilder.ApplyConfiguration(new KsefRateLimitStateConfig());
        modelBuilder.ApplyConfiguration(new KsefPurchaseInvoiceConfig());
        modelBuilder.ApplyConfiguration(new KsefAutoSyncSettingsConfig());
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefSyncLockConfig : IEntityTypeConfiguration<KsefSyncLock>
{
    public void Configure(EntityTypeBuilder<KsefSyncLock> b)
    {
        b.ToTable("ksef_sync_lock", "app_core");

        b.HasKey(x => new { x.Environment, x.ContextNip });

        b.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasConversion<string>();

        b.Property(x => x.ContextNip).HasColumnName("context_nip");

        b.Property(x => x.LockedUntilUtc).HasColumnName("locked_until_utc");
        b.Property(x => x.LockedByUserId).HasColumnName("locked_by_user_id");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefSyncStateConfig : IEntityTypeConfiguration<KsefSyncState>
{
    public void Configure(EntityTypeBuilder<KsefSyncState> b)
    {
        b.ToTable("ksef_sync_state", "app_core");

        b.HasKey(x => new { x.Environment, x.ContextNip });

        b.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasConversion<string>();

        b.Property(x => x.ContextNip).HasColumnName("context_nip");

        b.Property(x => x.LastPermanentStorageHwmDate).HasColumnName("last_permanent_storage_hwm_date");
        b.Property(x => x.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc");
        b.Property(x => x.LastSuccessAtUtc).HasColumnName("last_success_at_utc");
        b.Property(x => x.LastError).HasColumnName("last_error");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}

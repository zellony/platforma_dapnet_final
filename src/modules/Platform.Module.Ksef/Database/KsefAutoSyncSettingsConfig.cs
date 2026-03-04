using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefAutoSyncSettingsConfig : IEntityTypeConfiguration<KsefAutoSyncSettings>
{
    public void Configure(EntityTypeBuilder<KsefAutoSyncSettings> b)
    {
        b.ToTable("ksef_auto_sync_settings", "app_core");

        b.HasKey(x => new { x.Environment, x.ContextNip });

        b.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasConversion<string>();

        b.Property(x => x.ContextNip).HasColumnName("context_nip");

        b.Property(x => x.Enabled).HasColumnName("enabled");
        b.Property(x => x.IntervalMinutes).HasColumnName("interval_minutes");

        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}

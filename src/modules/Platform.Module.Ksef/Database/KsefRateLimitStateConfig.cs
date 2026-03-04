using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefRateLimitStateConfig : IEntityTypeConfiguration<KsefRateLimitState>
{
    public void Configure(EntityTypeBuilder<KsefRateLimitState> b)
    {
        b.ToTable("ksef_rate_limit_state", "app_core");

        b.HasKey(x => new { x.UserId, x.Environment });

        b.Property(x => x.UserId).HasColumnName("user_id");

        b.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasConversion<string>();

        b.Property(x => x.BlockedUntilUtc).HasColumnName("blocked_until_utc");
        b.Property(x => x.Last429AtUtc).HasColumnName("last_429_at_utc");
        b.Property(x => x.RetryAfterSeconds).HasColumnName("retry_after_seconds");
        b.Property(x => x.LastDetails).HasColumnName("last_details");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}

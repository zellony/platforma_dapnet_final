using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Database;

public sealed class KsefUserCredentialConfig : IEntityTypeConfiguration<KsefUserCredential>
{
    public void Configure(EntityTypeBuilder<KsefUserCredential> b)
    {
        b.ToTable("ksef_user_credentials", "app_core");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");

        b.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasConversion<string>();

        b.Property(x => x.ActiveType)
            .HasColumnName("active_type")
            .HasConversion<string>();

        b.Property(x => x.CertCrt).HasColumnName("cert_crt");
        b.Property(x => x.CertKey).HasColumnName("cert_key");
        b.Property(x => x.CertKeyPasswordEnc).HasColumnName("cert_key_password_enc");
        b.Property(x => x.CertFingerprintSha256).HasColumnName("cert_fingerprint_sha256");

        b.Property(x => x.Token).HasColumnName("token");
        b.Property(x => x.TokenValidTo).HasColumnName("token_valid_to");

        b.Property(x => x.RefreshToken).HasColumnName("refresh_token");
        b.Property(x => x.RefreshTokenValidTo).HasColumnName("refresh_token_valid_to");

        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        b.HasIndex(x => new { x.UserId, x.Environment }).HasDatabaseName("ix_ksef_user_credentials_user_env");
    }
}

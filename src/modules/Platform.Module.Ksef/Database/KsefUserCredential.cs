using System;

namespace Platform.Api.Modules.KSeF.Entities;

public enum KsefEnvironment { TE, TR, PRD }
public enum KsefCredentialType { CERT, TOKEN }

public sealed class KsefUserCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public KsefEnvironment Environment { get; set; }
    public KsefCredentialType ActiveType { get; set; }

    public byte[]? CertCrt { get; set; }
    public byte[]? CertKey { get; set; }
    public byte[]? CertKeyPasswordEnc { get; set; }
    public string? CertFingerprintSha256 { get; set; }

    public string? Token { get; set; }
    public DateTimeOffset? TokenValidTo { get; set; }

    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenValidTo { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

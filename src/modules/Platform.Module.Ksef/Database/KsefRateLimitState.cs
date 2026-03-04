using System;

namespace Platform.Api.Modules.KSeF.Entities;

public sealed class KsefRateLimitState
{
    public Guid UserId { get; set; }
    public KsefEnvironment Environment { get; set; }

    public DateTimeOffset? BlockedUntilUtc { get; set; }
    public DateTimeOffset? Last429AtUtc { get; set; }
    public int? RetryAfterSeconds { get; set; }

    public string? LastDetails { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

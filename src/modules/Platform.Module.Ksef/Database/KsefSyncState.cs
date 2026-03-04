using System;

namespace Platform.Api.Modules.KSeF.Entities;

public sealed class KsefSyncState
{
    public KsefEnvironment Environment { get; set; }
    public string ContextNip { get; set; } = default!;

    public DateTimeOffset? LastPermanentStorageHwmDate { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? LastSuccessAtUtc { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

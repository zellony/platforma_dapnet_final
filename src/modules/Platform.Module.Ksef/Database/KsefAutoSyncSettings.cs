using System;

namespace Platform.Api.Modules.KSeF.Entities;

public sealed class KsefAutoSyncSettings
{
    public KsefEnvironment Environment { get; set; }
    public string ContextNip { get; set; } = default!;

    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

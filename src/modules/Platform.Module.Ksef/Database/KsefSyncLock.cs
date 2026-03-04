using System;

namespace Platform.Api.Modules.KSeF.Entities;

public sealed class KsefSyncLock
{
    public KsefEnvironment Environment { get; set; }
    public string ContextNip { get; set; } = default!;

    public DateTimeOffset LockedUntilUtc { get; set; }
    public Guid LockedByUserId { get; set; }
}

using System;

namespace Platform.Kernel.Common;

public static class PlatformVersion
{
    public const string Current = "0.1.0";
    public const string Name = "DAPNET Platform";
    
    // ✅ DATA WYDANIA TEJ WERSJI (używana do weryfikacji prawa do aktualizacji)
    public static readonly DateTime ReleaseDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
}

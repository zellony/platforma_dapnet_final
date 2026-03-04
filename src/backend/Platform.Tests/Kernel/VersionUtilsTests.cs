using FluentAssertions;
using Platform.Kernel.Common;
using Xunit;

namespace Platform.Tests.Kernel;

public class VersionUtilsTests
{
    [Fact]
    public void GetApplicationVersion_ShouldReturnVersionOrUnknown()
    {
        // Act
        var version = VersionUtils.GetApplicationVersion();

        // Assert
        version.Should().NotBeNullOrWhiteSpace();
        // W kontekście testów Assembly.GetEntryAssembly() może być null lub test runnerem,
        // więc "unknown" jest również poprawnym wynikiem w tym scenariuszu,
        // chyba że skonfigurujemy test runnera inaczej.
        // Ale testujemy, czy metoda nie rzuca wyjątku.
    }
}

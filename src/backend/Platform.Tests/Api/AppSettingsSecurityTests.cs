using FluentAssertions;
using Xunit;

namespace Platform.Tests.Api;

public class AppSettingsSecurityTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void AppSettings_ShouldNotContainLegacySecrets(string fileName)
    {
        var content = File.ReadAllText(ResolveApiSettingsPath(fileName));

        content.Should().NotContain("\"SuperAdmin\"", "legacy backdoor config must stay removed");
        content.Should().NotContain("GreenIsTheBest", "legacy plaintext must never return");
        content.Should().NotContain("\"PasswordHash\"", "service account hash must not be stored in appsettings");
        content.Should().NotContain("\"Key\":", "JWT signing key must not be stored in appsettings");
    }

    private static string ResolveApiSettingsPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Platform.Api", fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Cannot locate {fileName} near {AppContext.BaseDirectory}");
    }
}

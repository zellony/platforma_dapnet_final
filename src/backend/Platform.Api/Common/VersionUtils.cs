using System.Reflection;

namespace Platform.Api.Common;

public static class VersionUtils
{
    public static string GetApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null) return "unknown";

        return assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
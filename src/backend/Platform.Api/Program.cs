using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Platform.Api.Startup;
using Platform.Kernel.Core.RBAC;

var totalStartupSw = Stopwatch.StartNew();
using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
}));
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Startup");
bootstrapLogger.LogInformation("[STARTUP] Rozpoczecie inicjalizacji API: {Timestamp}", DateTime.Now.ToString("HH:mm:ss.fff"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var instanceIdArg = args.FirstOrDefault(a => a.StartsWith("--instance-id=", StringComparison.OrdinalIgnoreCase));
var instanceId = instanceIdArg?.Substring("--instance-id=".Length)?.Trim();
if (string.IsNullOrWhiteSpace(instanceId)) instanceId = Guid.NewGuid().ToString("N");

var (isConfigured, modulesPath) = builder.AddPlatformServices(args, instanceId);
var isDbServiceMode = args.Any(a => string.Equals(a, "--db-service-mode", StringComparison.OrdinalIgnoreCase));

var app = builder.Build();
await app.InitializePlatformAsync(isConfigured, isDbServiceMode);
app.UsePlatformPipeline(isConfigured, modulesPath);

var runtimeInfoPath = Path.Combine(AppContext.BaseDirectory, "runtime.json");
var sharedRuntimeDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "PlatformaDapnet"
);
var sharedRuntimeInfoPath = Path.Combine(sharedRuntimeDir, "runtime.json");
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var urls = app.Urls.ToArray();
        var preferred = urls.FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                       ?? urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                       ?? "https://127.0.0.1:5001";
        preferred = NormalizeLoopbackUrl(preferred);

        var payload = JsonSerializer.Serialize(new
        {
            preferredBaseUrl = preferred,
            urls = urls.Select(NormalizeLoopbackUrl).ToArray(),
            startedAtUtc = DateTime.UtcNow
        });

        WriteAllTextAtomic(runtimeInfoPath, payload);

        Directory.CreateDirectory(sharedRuntimeDir);
        WriteAllTextAtomic(sharedRuntimeInfoPath, payload);

        app.Logger.LogInformation("[STARTUP] Runtime URL zapisany: {PreferredUrl}", preferred);
        app.Logger.LogInformation("[STARTUP] runtime.json (local): {LocalPath}", runtimeInfoPath);
        app.Logger.LogInformation("[STARTUP] runtime.json (shared): {SharedPath}", sharedRuntimeInfoPath);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "[STARTUP] Nie udalo sie zapisac runtime.json.");
    }
});

app.Logger.LogInformation("[STARTUP] API gotowe do pracy. Calkowity czas: {ElapsedMs}ms", totalStartupSw.ElapsedMilliseconds);
app.Run();

static string NormalizeLoopbackUrl(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
    var host = uri.Host;
    if (host == "localhost" || host == "::1" || host == "[::1]")
    {
        var builder = new UriBuilder(uri) { Host = "127.0.0.1" };
        return builder.Uri.ToString().TrimEnd('/');
    }
    return url.TrimEnd('/');
}

static void WriteAllTextAtomic(string destinationPath, string content)
{
    var directory = Path.GetDirectoryName(destinationPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var tempPath = destinationPath + ".tmp";
    File.WriteAllText(tempPath, content, new UTF8Encoding(false));
    File.Move(tempPath, destinationPath, overwrite: true);
}

public static class JwtKeyVault
{
    public static string CurrentKey = string.Empty;
}

public static class RsaKeyVault
{
    private static readonly RSA Rsa = RSA.Create(2048);

    public static string GetPublicKey() => Convert.ToBase64String(Rsa.ExportSubjectPublicKeyInfo());

    public static string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        var decrypted = Rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        return Encoding.UTF8.GetString(decrypted);
    }
}

public class NoOpPermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        => Task.CompletedTask;
}

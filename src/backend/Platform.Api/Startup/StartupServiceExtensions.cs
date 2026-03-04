using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Platform.Api.Core.Auth;
using Platform.Api.Core.Licensing;
using Platform.Api.Core.RBAC;
using Platform.Api.Infrastructure;
using Platform.Api.Infrastructure.Config;
using Platform.Api.Infrastructure.Database;
using Platform.Api.Infrastructure.Modules;
using Platform.Kernel.Core.Auth;
using Platform.Kernel.Core.RBAC;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Startup;

public static class StartupServiceExtensions
{
    private const string AppCorsPolicy = "AppCorsPolicy";
    private static readonly ILogger StartupLogger = LoggerFactory
        .Create(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }))
        .CreateLogger("Startup");

    public static (bool IsConfigured, string ModulesPath) AddPlatformServices(this WebApplicationBuilder builder, string[] args, string instanceId)
    {
        InitializeJwtKey();
        ConfigureProductionLogging(builder);
        ConfigureKestrel(builder);
        ConfigureConfiguration(builder, args);
        InitializeServiceAccountSecrets(builder);

        var isConfigured = ConfigureDatabase(builder);
        var modulesPath = ConfigureModules(builder);

        builder.Services.AddHttpClient();
        ConfigureCors(builder);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Platform.Api",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Platform.Client",
                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                {
                    return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKeyVault.CurrentKey)) };
                }
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.ContainsKey("dapnet_session"))
                    {
                        context.Token = context.Request.Cookies["dapnet_session"];
                    }
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, SuperAdminBypassHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddSingleton(new RuntimeInstanceContext(instanceId));

        builder.Services.AddHostedService<ParentProcessMonitor>();
        builder.Services.AddHostedService<SessionCleanupService>();
        builder.Services.AddScoped<LicenseService>();

        return (isConfigured, modulesPath);
    }

    private static void InitializeServiceAccountSecrets(WebApplicationBuilder builder)
    {
        try
        {
            var login = ServiceAccountSecretsProvider.GetLogin();
            var hash = ServiceAccountSecretsProvider.GetPasswordHash();

            if (string.IsNullOrWhiteSpace(login))
                throw new InvalidOperationException("Login konta serwisowego jest pusty.");
            if (string.IsNullOrWhiteSpace(hash))
                throw new InvalidOperationException("Hash hasla konta serwisowego jest pusty.");

            // Runtime ma stale zrodlo konta serwisowego z kodu.
            builder.Configuration["ServiceAccount:Login"] = login;
            builder.Configuration["ServiceAccount:PasswordHash"] = hash;
            StartupLogger.LogInformation("[STARTUP] Dane konta serwisowego zaladowane z provider'a kodowego.");
        }
        catch (Exception ex)
        {
            StartupLogger.LogError(ex, "[STARTUP_FATAL] Nie udalo sie zainicjalizowac danych konta serwisowego.");
            throw;
        }
    }

    private static void ConfigureCors(WebApplicationBuilder builder)
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var normalizedOrigins = configuredOrigins
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allowNullOrigin = builder.Configuration.GetValue<bool?>("Cors:AllowNullOrigin");
        var isDevelopment = builder.Environment.IsDevelopment();
        var defaultDevOrigins = new[] { "http://127.0.0.1:5173", "http://localhost:5173" };
        var effectiveOrigins = isDevelopment && normalizedOrigins.Length == 0 ? defaultDevOrigins : normalizedOrigins;
        var effectiveAllowNullOrigin = allowNullOrigin ?? !isDevelopment;

        if (!isDevelopment && effectiveOrigins.Length == 0 && !effectiveAllowNullOrigin)
        {
            throw new InvalidOperationException(
                "Brak bezpiecznej konfiguracji CORS dla produkcji. Ustaw Cors:AllowNullOrigin=true (desktop) lub podaj Cors:AllowedOrigins."
            );
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(AppCorsPolicy, policy =>
            {
                policy.AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetIsOriginAllowed(origin =>
                      {
                          if (string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
                              return effectiveAllowNullOrigin;

                          return effectiveOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));
                      });
            });
        });

        var originsInfo = effectiveOrigins.Length == 0 ? "(none)" : string.Join(", ", effectiveOrigins);
        StartupLogger.LogInformation(
            "[STARTUP] CORS policy: {Policy}; allowNullOrigin={AllowNullOrigin}; origins={Origins}",
            AppCorsPolicy,
            effectiveAllowNullOrigin,
            originsInfo);
    }

    private static void ConfigureProductionLogging(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsProduction()) return;

        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
    }

    private static void InitializeJwtKey()
    {
        try
        {
            JwtKeyVault.CurrentKey = UserConfigStore.LoadOrCreateJwtSigningKey();
            if (string.IsNullOrWhiteSpace(JwtKeyVault.CurrentKey))
                throw new InvalidOperationException("Klucz JWT jest pusty.");

            StartupLogger.LogInformation("[STARTUP] Klucz JWT zaladowany z magazynu lokalnego.");
        }
        catch (Exception ex)
        {
            StartupLogger.LogError(ex, "[STARTUP_FATAL] Nie udalo sie zainicjalizowac klucza JWT.");
            throw;
        }
    }

    private static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        var sslSw = Stopwatch.StartNew();
        builder.WebHost.ConfigureKestrel(options =>
        {
            var cert = SslCertificateProvider.GetOrCreateCertificate();
            // Dynamic ports avoid conflicts on customer machines.
            options.Listen(System.Net.IPAddress.Loopback, 0);
            options.Listen(System.Net.IPAddress.Loopback, 0, listenOptions => { listenOptions.UseHttps(cert); });
        });
        StartupLogger.LogInformation("[STARTUP] Konfiguracja Kestrel & SSL: {ElapsedMs}ms", sslSw.ElapsedMilliseconds);
    }

    private static void ConfigureConfiguration(WebApplicationBuilder builder, string[] args)
    {
        builder.Configuration.AddCommandLine(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables();
    }

    private static bool ConfigureDatabase(WebApplicationBuilder builder)
    {
        var dbConfigSw = Stopwatch.StartNew();
        string? mainConnectionString = null;
        try { mainConnectionString = UserConfigStore.LoadConnectionString(); } catch { }
        var configFileExists = UserConfigStore.ConfigExists();
        var isConfigured = !string.IsNullOrWhiteSpace(mainConnectionString);

        if (configFileExists && !isConfigured)
        {
            StartupLogger.LogWarning("[STARTUP_WARNING] Wykryto config.json, ale nie udalo sie odczytac connection string.");
        }

        if (isConfigured)
        {
            var cb = new NpgsqlConnectionStringBuilder(mainConnectionString);
            cb.ApplicationName = $"PlatformaDAPNET | {Environment.MachineName}";
            mainConnectionString = cb.ToString();
            builder.Configuration["ConnectionStrings:Main"] = mainConnectionString;
        }

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (isConfigured)
            {
                options.UseNpgsql(mainConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "app_core"));
            }
            else
            {
                options.UseNpgsql("Host=localhost;Database=temp;Timeout=2;CommandTimeout=2",
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "app_core"));
            }
        });
        StartupLogger.LogInformation("[STARTUP] Konfiguracja Bazy Danych: {ElapsedMs}ms", dbConfigSw.ElapsedMilliseconds);
        return isConfigured;
    }

    private static string ConfigureModules(WebApplicationBuilder builder)
    {
        var modulesSw = Stopwatch.StartNew();
        var modulesPath = Path.Combine(AppContext.BaseDirectory, "modules");
        var mvcBuilder = builder.Services.AddControllers();
        ModuleLoader.LoadFromDirectory(modulesPath, mvcBuilder);
        ModuleLoader.RegisterAll(builder.Services, builder.Configuration, modulesPath);
        StartupLogger.LogInformation("[STARTUP] Ladowanie Modulow: {ElapsedMs}ms", modulesSw.ElapsedMilliseconds);
        return modulesPath;
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database.Seed;
using Platform.Api.Infrastructure.Modules;
using Platform.Kernel.Core.Config.Entities;
using Microsoft.Extensions.Configuration;
using Platform.Api.Infrastructure.Config;
using Npgsql;
using Platform.Kernel.Common;
using Platform.Api.Core.Licensing;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Infrastructure.Database;

public static class DatabaseInitializer
{
    private const string GlobalVersionKey = "System_SchemaVersion";
    private const string ModuleVersionPrefix = "ModuleVersion_";

    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        string? cs = UserConfigStore.LoadConnectionString();
        if (string.IsNullOrWhiteSpace(cs)) return;

        var cb = new NpgsqlConnectionStringBuilder(cs);
        cb.ApplicationName = $"PlatformaDAPNET | {Environment.MachineName}";
        await EnsureTargetDatabaseExistsAsync(cb);
        cs = cb.ToString();
        configuration["ConnectionStrings:Main"] = cs;

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var licenseService = scope.ServiceProvider.GetRequiredService<LicenseService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DatabaseInitializer");

        try
        {
            db.Database.GetDbConnection().ConnectionString = cs;

            // KROK 1: Zapewnienie istnienia schematu i tabel (MIGRACJE)
            // To musi być pierwsze, aby uniknąć błędów "relation does not exist"
            logger.LogInformation("[INIT] Running migrations...");
            await db.Database.MigrateAsync();
            
            // KROK 2: Zapewnienie istnienia tabel konfiguracyjnych (Safety check)
            db.EnsureConfigTablesExist();

            // KROK 3: Teraz, gdy tabele ISTNIEJĄ, możemy bezpiecznie czytać dane
            Dictionary<string, string> versionConfigs = new();
            try 
            {
                versionConfigs = await db.SystemConfigs.AsNoTracking()
                    .Where(x => x.Key == GlobalVersionKey || x.Key.StartsWith(ModuleVersionPrefix))
                    .ToDictionaryAsync(x => x.Key, x => x.Value);
            }
            catch { logger.LogWarning("[INIT_WARNING] Could not read system_configs, assuming fresh install."); }

            // KROK 4: Sprawdzenie licencji pod kątem aktualizacji
            var licenseStatus = await licenseService.GetCurrentStatusAsync();
            bool canMigrate = !licenseStatus.IsUpdateExpired;

            // KROK 5: Seeding Rdzenia
            versionConfigs.TryGetValue(GlobalVersionKey, out var currentDbVersion);
            if (currentDbVersion != PlatformVersion.Current)
            {
                if (canMigrate)
                {
                    logger.LogInformation("[INIT] Seeding Core to version {Version}...", PlatformVersion.Current);
                    await new AppDbSeeder(db, configuration).SeedAsync();
                    await UpdateConfigVersion(db, GlobalVersionKey, PlatformVersion.Current);
                }
            }

            // KROK 6: Migracje i Seedy Modułów
            await RunSmartModuleMigrationsAsync(scope.ServiceProvider, cs, versionConfigs, db, canMigrate);

            // KROK 7: Ładowanie klucza JWT
            await LoadJwtKeyAsync(db);
            
            logger.LogInformation("[INIT] Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[INIT_FATAL_ERROR] Database initialization failed.");
            throw;
        }
    }

    private static async Task RunSmartModuleMigrationsAsync(IServiceProvider sp, string connectionString, Dictionary<string, string> dbVersions, AppDbContext mainDb, bool canMigrate)
    {
        foreach (var module in ModuleLoader.LoadedModules)
        {
            string versionKey = $"{ModuleVersionPrefix}{module.Name}";
            string moduleVersion = module.Version.ToString();
            dbVersions.TryGetValue(versionKey, out var lastAppliedVersion);

            if (lastAppliedVersion != moduleVersion && canMigrate)
            {
                var moduleAssembly = module.GetType().Assembly;
                var dbContextType = moduleAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(AppDbContext));

                if (dbContextType != null)
                {
                    var ctx = sp.GetService(dbContextType) as DbContext;
                    if (ctx != null)
                    {
                        ctx.Database.GetDbConnection().ConnectionString = connectionString;
                        await ctx.Database.MigrateAsync();
                    }
                }
                await RunSpecificModuleSeederAsync(sp, moduleAssembly);
                await UpdateConfigVersion(mainDb, versionKey, moduleVersion);
            }
        }
    }

    private static async Task RunSpecificModuleSeederAsync(IServiceProvider sp, Assembly moduleAssembly)
    {
        var seederType = moduleAssembly.GetTypes()
            .FirstOrDefault(t => !t.IsAbstract && !t.IsInterface && t.Name.EndsWith("PermissionSeeder", StringComparison.Ordinal));

        if (seederType != null)
        {
            try
            {
                var instance = ActivatorUtilities.CreateInstance(sp, seederType);
                var method = seederType.GetMethod("SeedAsync", BindingFlags.Instance | BindingFlags.Public);
                if (method != null) await (Task)method.Invoke(instance, null)!;
            }
            catch { }
        }
    }

    private static async Task UpdateConfigVersion(AppDbContext db, string key, string value)
    {
        var config = await db.SystemConfigs.FirstOrDefaultAsync(x => x.Key == key);
        if (config == null)
            db.SystemConfigs.Add(new SystemConfig { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow });
        else
        {
            config.Value = value;
            config.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private static async Task LoadJwtKeyAsync(AppDbContext db)
    {
        try 
        {
            var config = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "JwtSigningKey");
            if (config != null) {
                // Logika dekryptowania klucza...
            }
        }
        catch { }
    }

    private static async Task EnsureTargetDatabaseExistsAsync(NpgsqlConnectionStringBuilder targetBuilder)
    {
        var targetDb = targetBuilder.Database;
        if (string.IsNullOrWhiteSpace(targetDb))
            throw new InvalidOperationException("Brak nazwy bazy danych w connection string.");

        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(targetBuilder.ConnectionString)
        {
            Database = string.IsNullOrWhiteSpace(targetBuilder.Database) ? "postgres" : "postgres"
        };

        await using var conn = new NpgsqlConnection(maintenanceBuilder.ToString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @dbName;";
        cmd.Parameters.AddWithValue("dbName", targetDb);
        var exists = await cmd.ExecuteScalarAsync();

        if (exists == null)
        {
            throw new InvalidOperationException(
                $"Docelowa baza danych '{targetDb}' nie istnieje. Utworz baze recznie przed uruchomieniem inicjalizacji."
            );
        }
    }
}

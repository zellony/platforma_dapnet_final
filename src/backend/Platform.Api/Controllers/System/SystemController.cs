using Microsoft.AspNetCore.Mvc;
using Platform.Api.Infrastructure.Config;
using Platform.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Npgsql;
using Platform.Api.Core.Licensing;
using Microsoft.AspNetCore.Authorization;
using Platform.Kernel.Common;
using Platform.Api.Infrastructure.Modules;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Platform.Api.Controllers.System;

[ApiController]
[Route("system")]
public class SystemController : ControllerBase
{
    private readonly IServiceProvider _sp;
    private readonly LicenseService _licenseService;
    private readonly IConfiguration _config;

    public SystemController(IServiceProvider sp, LicenseService licenseService, IConfiguration config)
    {
        _sp = sp;
        _licenseService = licenseService;
        _config = config;
    }

    private AppDbContext? GetDb() => _sp.GetService(typeof(AppDbContext)) as AppDbContext;

    [HttpGet("rsa-key")]
    [AllowAnonymous]
    public IActionResult GetRsaPublicKey()
    {
        return Ok(new { publicKey = RsaKeyVault.GetPublicKey() });
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status()
    {
        var hasConfig = UserConfigStore.ConfigExists();
        string? configuredConnection = null;
        try { configuredConnection = UserConfigStore.LoadConnectionString(); } catch { }
        
        if (!hasConfig)
        {
            return Ok(new { 
                ok = true, 
                databaseName = "Brak konfiguracji", 
                activeUsersCount = 0, 
                setupRequired = true,
                dbState = "DB_CONFIG_MISSING",
                licenseRequired = false,
                licenseExpired = false,
                adminRequired = false,
                version = PlatformVersion.Current,
                releaseDate = PlatformVersion.ReleaseDate
            });
        }

        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            return Ok(new
            {
                ok = false,
                databaseName = "Bledna konfiguracja",
                setupRequired = false,
                dbState = "DB_CONFIG_BROKEN",
                error = "Plik config.json istnieje, ale nie mozna odczytac poprawnego connection string."
            });
        }

        string dbName = "Nieznana";
        var db = GetDb();
        bool licenseRequired = false;
        bool licenseExpired = false;
        bool adminRequired = false;

        if (db != null) 
        { 
            try 
            { 
                db.Database.SetCommandTimeout(2);
                dbName = db.Database.GetDbConnection().Database; 
                
                // Sprawdzamy czy tabela system_configs istnieje (wskaźnik czy migracje przeszły)
                var conn = db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'app_core' AND table_name = 'system_configs')";
                var tablesExist = (bool)(await cmd.ExecuteScalarAsync() ?? false);

                if (!tablesExist)
                {
                    return Ok(new { 
                        ok = true, 
                        databaseName = dbName, 
                        setupRequired = true, // Wymagamy inicjalizacji (migracji)
                        dbState = "DB_SCHEMA_MISSING",
                        licenseRequired = false,
                        adminRequired = false,
                        version = PlatformVersion.Current
                    });
                }

                var licenseStatus = await _licenseService.GetCurrentStatusAsync();
                
                if (licenseStatus.Message == "Brak licencji") licenseRequired = true;
                else if (!licenseStatus.IsActive) licenseExpired = true;

                var superAdminLogin = (_config["ServiceAccount:Login"] ?? "AdminDAPNET").Trim();
                var anyClientAdmin = await db.Users.AnyAsync(u => u.Login.ToLower() != superAdminLogin.ToLower());
                adminRequired = !anyClientAdmin;
            } 
            catch (Exception ex)
            {
                return Ok(new { 
                    ok = false, 
                    databaseName = "Błąd połączenia", 
                    setupRequired = false,
                    dbState = "DB_UNAVAILABLE",
                    error = $"Nie można połączyć się z bazą danych: {ex.Message}"
                });
            } 
        }
        
        return Ok(new { 
            ok = true, 
            databaseName = dbName, 
            activeUsersCount = 1, 
            setupRequired = false,
            dbState = "DB_READY",
            licenseRequired = licenseRequired,
            licenseExpired = licenseExpired,
            adminRequired = adminRequired,
            version = PlatformVersion.Current,
            releaseDate = PlatformVersion.ReleaseDate
        });
    }

    [HttpPost("init")]
    [AllowAnonymous]
    public async Task<IActionResult> Initialize()
    {
        try
        {
            await DatabaseInitializer.InitializeAsync(_sp, _config);
            return Ok(new { ok = true, message = "System zainicjalizowany pomyślnie." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = $"Błąd inicjalizacji: {ex.Message}" });
        }
    }

    [HttpGet("info")]
    [Authorize]
    public IActionResult GetSystemInfo()
    {
        var modules = ModuleLoader.LoadedModules.Select(m => new {
            name = m.Name,
            version = m.Version
        }).ToList();

        var apiVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        
        string dbName = "Brak połączenia";
        var db = GetDb();
        if (db != null) {
            try { dbName = db.Database.GetDbConnection().Database; } catch { }
        }

        return Ok(new {
            platformVersion = PlatformVersion.Current,
            releaseDate = PlatformVersion.ReleaseDate,
            apiVersion = apiVersion,
            databaseName = dbName,
            modules = modules,
            os = Environment.OSVersion.ToString(),
            dotnetVersion = Environment.Version.ToString()
        });
    }

    [HttpPost("config/db")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveDbConfig([FromBody] SaveDbConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConnectionString)) return BadRequest("ConnectionString is required.");
        if (UserConfigStore.ConfigExists() && !IsCurrentUserWindowsAdministrator())
        {
            return StatusCode(403, new { message = "Zmiana konfiguracji bazy danych wymaga uruchomienia aplikacji jako administrator Windows (tryb serwisowy)." });
        }
        try {
            UserConfigStore.SaveConnectionString(dto.ConnectionString);
            // Nie wywołujemy tu inicjalizacji, bo frontend zrobi to osobnym krokiem /init
            return Ok(new { saved = true });
        } catch (Exception ex) {
            return StatusCode(500, new { message = $"Błąd zapisu konfiguracji: {ex.Message}" });
        }
    }

    [HttpGet("config/db")]
    [AllowAnonymous]
    public IActionResult GetDbConfig()
    {
        if (!IsCurrentUserWindowsAdministrator())
        {
            return StatusCode(403, new { message = "Dostep tylko dla administratora Windows." });
        }

        string? cs = null;
        try { cs = UserConfigStore.LoadConnectionString(); } catch { }
        if (string.IsNullOrWhiteSpace(cs))
        {
            return NotFound(new { message = "Brak zapisanej konfiguracji bazy danych." });
        }

        try
        {
            var cb = new NpgsqlConnectionStringBuilder(cs);
            return Ok(new
            {
                host = cb.Host ?? "",
                port = cb.Port > 0 ? cb.Port : 5432,
                database = cb.Database ?? "",
                username = cb.Username ?? "",
                password = cb.Password ?? ""
            });
        }
        catch
        {
            return StatusCode(500, new { message = "Nie udalo sie odczytac zapisanej konfiguracji bazy danych." });
        }
    }

    [HttpPost("db/ping")]
    [AllowAnonymous]
    public async Task<IActionResult> Ping([FromBody] PingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConnectionString)) return BadRequest("ConnectionString is required.");
        try
        {
            var cb = new NpgsqlConnectionStringBuilder(dto.ConnectionString);
            cb.Timeout = 5;
            using var conn = new NpgsqlConnection(cb.ToString());
            await conn.OpenAsync();
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, error = ex.Message });
        }
    }

    public record SaveDbConfigDto(string ConnectionString);
    public record PingDto(string ConnectionString);

    private static bool IsCurrentUserWindowsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    [HttpGet("db-info")]
    [Authorize]
    public async Task<IActionResult> GetDbInfo()
    {
        var db = GetDb();
        if (db == null) return BadRequest("Baza nie jest skonfigurowana.");
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT current_database() as db_name, version() as version, pg_size_pretty(pg_database_size(current_database())) as size, (SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND application_name LIKE 'PlatformaDAPNET%') as active_sessions, inet_server_addr() as server_ip, inet_server_port() as server_port";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Ok(new { databaseName = reader["db_name"].ToString(), version = reader["version"].ToString(), size = reader["size"].ToString(), activeSessions = reader["active_sessions"].ToString(), serverAddress = $"{reader["server_ip"]}:{reader["server_port"]}" });
            }
        } catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        return NotFound();
    }

    [HttpGet("db-sessions")]
    [Authorize]
    public async Task<IActionResult> GetDbSessions()
    {
        var db = GetDb();
        if (db == null) return BadRequest("Baza nie jest skonfigurowana.");
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT pid, client_addr as ip, application_name as app_name, backend_start as start_time, state, query_start as last_query_time FROM pg_stat_activity WHERE datname = current_database() AND application_name LIKE 'PlatformaDAPNET%' ORDER BY backend_start DESC";
            var sessions = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string appName = reader["app_name"].ToString() ?? "";
                string machineName = appName.Contains("|") ? appName.Split('|')[1].Trim() : "Unknown";
                sessions.Add(new { pid = reader["pid"].ToString(), ip = reader["ip"]?.ToString() ?? "local", machineName, startTime = reader["start_time"].ToString(), state = reader["state"].ToString(), lastQueryTime = reader["last_query_time"].ToString() });
            }
            return Ok(sessions);
        } catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}


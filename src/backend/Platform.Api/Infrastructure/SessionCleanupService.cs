using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database;
using Platform.Api.Infrastructure.Config;

namespace Platform.Api.Infrastructure;

public sealed class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(IServiceProvider serviceProvider, ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Czekamy chwilę na start aplikacji
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Jeśli nie ma konfiguracji bazy, nie robimy nic
            if (!UserConfigStore.ConfigExists())
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Sprawdzamy czy tabela user_sessions w ogóle istnieje (uodpornienie na brak migracji)
                var tableExists = await CheckTableExists(db, "app_core", "user_sessions");
                if (!tableExists)
                {
                    _logger.LogWarning("Table 'app_core.user_sessions' does not exist yet. Skipping cleanup.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                // Sesja wygasa po 15 minutach braku aktywności
                var timeoutLimit = DateTime.UtcNow.AddMinutes(-15);

                var staleSessions = await db.UserSessions
                    .Include(s => s.User)
                    .Where(s => s.LogoutAtUtc == null && 
                               (s.User.LastActivityAtUtc == null || s.User.LastActivityAtUtc < timeoutLimit))
                    .ToListAsync(stoppingToken);

                if (staleSessions.Count > 0)
                {
                    foreach (var session in staleSessions)
                    {
                        session.LogoutAtUtc = session.User.LastActivityAtUtc ?? DateTime.UtcNow;
                        session.LogoutReason = "Timeout";
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Closed {Count} stale sessions.", staleSessions.Count);
                }
            }
            catch (Exception ex)
            {
                // Logujemy błąd tylko jeśli baza powinna być dostępna i nie jest to błąd braku tabeli
                if (UserConfigStore.ConfigExists() && !ex.Message.Contains("does not exist"))
                {
                    _logger.LogError(ex, "Error during session cleanup.");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task<bool> CheckTableExists(AppDbContext db, string schema, string table)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)";
            
            var pSchema = cmd.CreateParameter();
            pSchema.ParameterName = "@schema";
            pSchema.Value = schema;
            cmd.Parameters.Add(pSchema);

            var pTable = cmd.CreateParameter();
            pTable.ParameterName = "@table";
            pTable.Value = table;
            cmd.Parameters.Add(pTable);

            return (bool)(await cmd.ExecuteScalarAsync() ?? false);
        }
        catch { return false; }
    }
}

using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database;

namespace Platform.Api.Infrastructure;

public sealed class ParentProcessMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ParentProcessMonitor> _logger;
    private readonly RuntimeInstanceContext _runtimeInstance;

    public ParentProcessMonitor(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime, ILogger<ParentProcessMonitor> logger, RuntimeInstanceContext runtimeInstance)
    {
        _serviceProvider = serviceProvider;
        _lifetime = lifetime;
        _logger = logger;
        _runtimeInstance = runtimeInstance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Czekamy na zamknięcie strumienia wejściowego (zamknięcie Electrona)
        await Task.Run(() => {
            try {
                Console.In.Read(); 
            } catch { }
        }, stoppingToken);

        // WYLOGOWANIE
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE app_core.user_sessions " +
                "SET logout_at_utc = now(), logout_reason = 'AppClose' " +
                "WHERE logout_at_utc IS NULL AND (instance_id = {0} OR (instance_id IS NULL AND user_agent LIKE {1}))",
                _runtimeInstance.InstanceId,
                $"%;instance={_runtimeInstance.InstanceId}%"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup sessions on exit.");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}

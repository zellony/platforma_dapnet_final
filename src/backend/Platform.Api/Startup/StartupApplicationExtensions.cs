using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure;
using Platform.Api.Infrastructure.Config;
using Platform.Api.Infrastructure.Database;
using Platform.Api.Infrastructure.Modules;

namespace Platform.Api.Startup;

public static class StartupApplicationExtensions
{
    private const string AppCorsPolicy = "AppCorsPolicy";

    public static async Task InitializePlatformAsync(this WebApplication app, bool isConfigured, bool isDbServiceMode)
    {
        if (!isConfigured) return;
        if (!isDbServiceMode)
        {
            app.Logger.LogInformation("[STARTUP] Pominieto automatyczna inicjalizacje/migracje bazy (tryb standardowy).");
            return;
        }

        var dbInitSw = Stopwatch.StartNew();
        try { await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration); } catch { }
        app.Logger.LogInformation("[STARTUP] Inicjalizacja/Migracja Bazy: {ElapsedMs}ms", dbInitSw.ElapsedMilliseconds);
    }

    public static void UsePlatformPipeline(this WebApplication app, bool isConfigured, string modulesPath)
    {
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors(AppCorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseReadOnlyMode();
        if (isConfigured) app.UseUserActivity();

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
        app.MapGet("/health/ready", async (IServiceProvider services) =>
        {
            var configFileExists = UserConfigStore.ConfigExists();
            if (!isConfigured)
            {
                if (configFileExists)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
                return Results.Ok(new { status = "ready", database = "not_configured" });
            }

            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Ok(new { status = "ready", database = "connected" });
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous();

        app.MapControllers();
        ModuleLoader.MapAll(app, modulesPath);
    }
}

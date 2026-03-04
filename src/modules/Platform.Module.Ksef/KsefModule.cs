using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

using Platform.Contracts;
using Platform.Api.Modules.KSeF.Auth;
using Platform.Api.Modules.KSeF.Crypto;
using Platform.Api.Modules.KSeF.Database;
using Platform.Api.Modules.KSeF.Http;
using Platform.Api.Modules.KSeF.Options;
using Platform.Api.Modules.KSeF.Services;

namespace Platform.Api.Modules.KSeF;

public sealed class KsefModule : IModule
{
    public string Name => "KSeF";

    public Version Version => new(1, 0, 0);

    public void RegisterServices(IServiceCollection services, IConfiguration config)
    {
        services.Configure<KsefOptions>(config.GetSection("KSeF"));

        // EF mapping for existing tables (single schema: app_core)
        var cs = config.GetConnectionString("Main") ?? "Host=localhost;Database=temp;Timeout=2";

        services.AddDbContext<KsefDbContext>(opt =>
            opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory_Ksef", "app_core")));

        // === KSeF schema gate (blocks only /ksef/* when DB migrations != DLL migrations) ===
        services.AddScoped<KsefSchemaCompatibilityFilter>();
        services.AddSingleton<IConfigureOptions<MvcOptions>, KsefSchemaCompatibilityMvcOptionsSetup>();

        services.AddScoped<IKsefHttpClient, KsefHttpClient>();
        services.AddScoped<KsefEncryptionService>();
        services.AddDataProtection();
        services.AddScoped<IKsefCredentialService, KsefCredentialService>();
        services.AddScoped<KsefAuthService>();

        // KSeF permissions seeder (module-owned)
        services.AddScoped<Platform.Module.Ksef.Database.Seed.KsefPermissionSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // KSeF currently exposes its API via MVC controllers contained in this module assembly.
        // The host automatically adds this assembly as an ApplicationPart when loading modules.
        // Nothing to map here.
    }
}

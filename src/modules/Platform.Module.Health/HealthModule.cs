using Microsoft.AspNetCore.Builder;          // <-- TO BYŁO BRAKUJĄCE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Contracts;

namespace Platform.Module.Health;

public sealed class HealthModule : IModule
{
    public string Name => "Health";
    public Version Version => new(1, 0, 0);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async context =>
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("OK");
        });
    }
}
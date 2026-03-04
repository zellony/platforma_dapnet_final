using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;


namespace Platform.Contracts;

public interface IModule
{
    /// <summary>
    /// Human-friendly module name (e.g. "KSeF").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Module version used for diagnostics and compatibility checks.
    /// </summary>
    Version Version { get; }

    void RegisterServices(IServiceCollection services, IConfiguration config);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

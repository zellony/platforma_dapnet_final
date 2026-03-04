using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Logging;
using Platform.Contracts;

namespace Platform.Api.Infrastructure.Modules;

public static class ModuleLoader
{
    private static readonly ILogger FallbackLogger = LoggerFactory
        .Create(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }))
        .CreateLogger("ModuleLoader");

    private static readonly object _lock = new();
    private static bool _loaded;
    private static IReadOnlyList<IModule> _modules = Array.Empty<IModule>();
    private static IReadOnlyList<Assembly> _moduleAssemblies = Array.Empty<Assembly>();

    public static IReadOnlyList<IModule> LoadedModules => _modules;
    public static IReadOnlyList<Assembly> LoadedAssemblies => _moduleAssemblies;

    private static void Info(ILogger? logger, string message, params object?[] args)
    {
        (logger ?? FallbackLogger).LogInformation(message, args);
    }

    private static void Warn(ILogger? logger, string message, params object?[] args)
    {
        (logger ?? FallbackLogger).LogWarning(message, args);
    }

    private static void Error(ILogger? logger, Exception ex, string message, params object?[] args)
    {
        (logger ?? FallbackLogger).LogError(ex, message, args);
    }

    /// <summary>
    /// Loads module assemblies from a "modules" directory next to the running app.
    /// Call once during startup, before building the app.
    /// </summary>
    public static void LoadFromDirectory(
        string modulesPath,
        IMvcBuilder mvc,
        ILogger? logger = null)
    {
        lock (_lock)
        {
            if (_loaded) return;

            var assemblies = new List<Assembly>();
            var modules = new List<IModule>();

            if (!Directory.Exists(modulesPath))
            {
                Info(logger, "Modules directory not found: {0}", modulesPath);
                _moduleAssemblies = assemblies;
                _modules = modules;
                _loaded = true;
                return;
            }

            Info(logger, "Scanning modules directory: {0}", modulesPath);

            foreach (var dll in Directory.EnumerateFiles(modulesPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    var full = Path.GetFullPath(dll);

                    // Avoid re-loading same assembly name
                    var asmName = AssemblyName.GetAssemblyName(full);
                    if (AppDomain.CurrentDomain.GetAssemblies().Any(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), asmName)))
                        continue;

                    Info(logger, "Loading module assembly: {0}", Path.GetFileName(full));

                    var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(full);
                    assemblies.Add(asm);

                    // If module contains MVC controllers, expose them to host
                    mvc.PartManager.ApplicationParts.Add(new AssemblyPart(asm));

                    var found = asm.GetTypes()
                        .Where(t => !t.IsAbstract && typeof(IModule).IsAssignableFrom(t))
                        .Select(t => (IModule)Activator.CreateInstance(t)!)
                        .ToList();

                    if (found.Count > 0)
                        modules.AddRange(found);
                }
                catch (Exception ex)
                {
                    Error(logger, ex, "Failed to load module from {0}", dll);
                }
            }

            _moduleAssemblies = assemblies;
            _modules = modules;
            _loaded = true;

            // Warn about duplicate module names (often indicates two versions present).
            var dups = modules.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();
            if (dups.Count > 0)
            {
                foreach (var g in dups)
                    Warn(logger, "Duplicate modules detected for '{0}': {1}", g.Key, string.Join(", ", g.Select(x => x.Version)));
            }

            if (modules.Count == 0)
                Info(logger, "No modules discovered in {0}", modulesPath);
            else
                Info(logger, "Loaded modules: {0}", string.Join(", ", modules.Select(m => $"{m.Name} v{m.Version}")));
        }
    }

    public static void RegisterAll(IServiceCollection services, IConfiguration config)
    {
        foreach (var module in _modules)
            module.RegisterServices(services, config);
    }

    public static void MapAll(IEndpointRouteBuilder endpoints)
    {
        foreach (var module in _modules)
            module.MapEndpoints(endpoints);
    }

    // Overloads that accept modulesPath for convenience (Program.cs can pass the same path used in LoadFromDirectory).
    // LoadFromDirectory must still be called once during startup (before RegisterAll/MapAll) to populate _modules.
    public static void RegisterAll(IServiceCollection services, IConfiguration config, string modulesPath)
        => RegisterAll(services, config);

    public static void MapAll(IEndpointRouteBuilder endpoints, string modulesPath)
        => MapAll(endpoints);
}

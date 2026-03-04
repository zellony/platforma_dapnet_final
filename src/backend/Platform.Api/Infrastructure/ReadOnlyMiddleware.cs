using Microsoft.Extensions.Logging;

namespace Platform.Api.Infrastructure;

public sealed class ReadOnlyMiddleware
{
    private static readonly HashSet<string> AllowedReadOnlyWriteEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST:/auth/login",
        "POST:/auth/logout",
        "POST:/auth/logout-beacon",
        "POST:/system/config/db",
        "POST:/system/db/ping",
        "POST:/license/upload"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ReadOnlyMiddleware> _logger;

    public ReadOnlyMiddleware(RequestDelegate next, ILogger<ReadOnlyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = (context.Request.Method ?? string.Empty).ToUpperInvariant();
        bool isModifyingRequest = method is "POST" or "PUT" or "DELETE" or "PATCH";

        if (isModifyingRequest)
        {
            var isReadOnlyClaim = context.User.FindFirst("is_read_only")?.Value;
            var userLogin = context.User.Identity?.Name ?? "Unknown";

            if (string.Equals(isReadOnlyClaim, "true", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedPath = NormalizePath(context.Request.Path.Value);
                var endpointKey = $"{method}:{normalizedPath}";
                var isAllowed = AllowedReadOnlyWriteEndpoints.Contains(endpointKey);

                if (!isAllowed)
                {
                    _logger.LogWarning("[ReadOnlyMode] BLOCKED {Method} {Path} for user {User}", method, normalizedPath, userLogin);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "System dziala w trybie TYLKO DO ODCZYTU z powodu wygaslej licencji. Nie mozna zapisywac zmian."
                    });
                    return;
                }

                _logger.LogInformation("[ReadOnlyMode] ALLOWED (whitelist) {Method} {Path} for user {User}", method, normalizedPath, userLogin);
            }
        }

        await _next(context);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var p = path.Trim();
        if (!p.StartsWith('/')) p = "/" + p;
        if (p.Length > 1) p = p.TrimEnd('/');
        return p.ToLowerInvariant();
    }
}

public static class ReadOnlyMiddlewareExtensions
{
    public static IApplicationBuilder UseReadOnlyMode(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ReadOnlyMiddleware>();
    }
}

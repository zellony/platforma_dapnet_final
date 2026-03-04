using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Infrastructure.Database;

namespace Platform.Api.Infrastructure;

public sealed class UserActivityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserActivityMiddleware> _logger;

    public UserActivityMiddleware(RequestDelegate next, ILogger<UserActivityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        bool shouldSkipActivityUpdate =
            path.StartsWith("/health/") ||
            path.StartsWith("/system/status") ||
            path.StartsWith("/system/config/db");

        if (!shouldSkipActivityUpdate && context.User.Identity?.IsAuthenticated == true)
        {
            var isSystemAdmin = context.User.HasClaim("is_system_admin", "true");
            if (!isSystemAdmin)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    try
                    {
                        await db.Users
                            .Where(u => u.Id == userId)
                            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActivityAtUtc, DateTime.UtcNow));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "User activity update skipped due to database error. Path={Path}", context.Request.Path);
                    }
                }
            }
        }

        await _next(context);
    }
}

public static class UserActivityMiddlewareExtensions
{
    public static IApplicationBuilder UseUserActivity(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserActivityMiddleware>();
    }
}

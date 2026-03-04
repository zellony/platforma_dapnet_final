using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Modules.KSeF.Database;
using Microsoft.AspNetCore.Http;

namespace Platform.Api.Modules.KSeF;

/// <summary>
/// Blocks only /ksef/* endpoints when the DB schema (applied migrations) doesn't match
/// the migrations available in the loaded KSeF module assembly.
/// </summary>
public sealed class KsefSchemaCompatibilityFilter : IAsyncActionFilter
{
    private static volatile bool _checked;
    private static volatile bool _ok;
    private static string? _details;
    private static readonly object _lock = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        // Apply only to KSeF endpoints
        if (!path.StartsWith("/ksef", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        EnsureChecked(context);

        if (!_ok)
        {
            context.Result = new ObjectResult(new
            {
                error = "KSEF_SCHEMA_MISMATCH",
                details = _details ?? "Schema mismatch between DB and KSeF module."
            })
            { StatusCode = StatusCodes.Status409Conflict };
            return;
        }

        await next();
    }

    private static void EnsureChecked(ActionExecutingContext context)
    {
        if (_checked) return;

        lock (_lock)
        {
            if (_checked) return;

            var db = context.HttpContext.RequestServices.GetService(typeof(KsefDbContext)) as KsefDbContext;
            if (db is null)
            {
                _ok = false;
                _details = "KsefDbContext not registered.";
                _checked = true;
                return;
            }

            // Compare: DB-applied vs DLL-available migrations
            var applied = db.Database.GetAppliedMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var available = db.Database.GetMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);

            var dbOnly = applied.Except(available).ToList();   // DB is ahead of DLL
            var dllOnly = available.Except(applied).ToList();  // DLL is ahead of DB

            if (dbOnly.Count == 0 && dllOnly.Count == 0)
            {
                _ok = true;
                _details = null;
            }
            else
            {
                _ok = false;
                _details =
                    $"DB-only migrations: [{string.Join(", ", dbOnly)}]; " +
                    $"DLL-only migrations: [{string.Join(", ", dllOnly)}].";
            }

            _checked = true;
        }
    }
}

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/sync")]
[Authorize]
public sealed class KsefSyncLockController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly string _connString;

    public KsefSyncLockController(IConfiguration cfg)
    {
        _cfg = cfg;
        _connString = cfg.GetConnectionString("Main")
            ?? throw new InvalidOperationException("Brak connection string 'Main'.");
    }

    // GET /ksef/sync/lock?env=TE
    [HttpGet("lock")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> GetLock([FromQuery] KsefEnvironment env, CancellationToken ct)
    {
        var envText = EnvToText(env);
        var contextNip = GetContextNip(env);

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
select locked_until_utc, locked_by_user_id
from app_core.ksef_sync_lock
where environment = @env and context_nip = @nip
limit 1;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", envText);
        cmd.Parameters.AddWithValue("nip", contextNip);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return Ok(new
            {
                environment = envText,
                contextNip,
                isLocked = false
            });
        }

        var lockedUntilObj = reader.GetValue(0);
        var lockedBy = reader.GetGuid(1);

        DateTimeOffset lockedUntil = lockedUntilObj switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => DateTimeOffset.MinValue
        };

        var now = DateTimeOffset.UtcNow;

        return Ok(new
        {
            environment = envText,
            contextNip,
            isLocked = lockedUntil > now,
            lockedUntilUtc = lockedUntil.ToString("O"),
            lockedByUserId = lockedBy
        });
    }

    // POST /ksef/sync/lock/force-unlock?env=TE
    [HttpPost("lock/force-unlock")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> ForceUnlock([FromQuery] KsefEnvironment env, CancellationToken ct)
    {
        var envText = EnvToText(env);
        var contextNip = GetContextNip(env);

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
delete from app_core.ksef_sync_lock
where environment = @env and context_nip = @nip;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", envText);
        cmd.Parameters.AddWithValue("nip", contextNip);

        await cmd.ExecuteNonQueryAsync(ct);

        return NoContent();
    }

    private string GetContextNip(KsefEnvironment env)
    {
        var envKey = EnvToText(env);
        var nip = _cfg[$"KSeF:Environments:{envKey}:ContextNip"];
        if (string.IsNullOrWhiteSpace(nip))
            throw new InvalidOperationException($"Brak KSeF:Environments:{envKey}:ContextNip w konfiguracji.");
        return nip.Trim();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
            throw new InvalidOperationException("Brak claim 'sub' w JWT.");
        return Guid.Parse(sub);
    }

    private static string EnvToText(KsefEnvironment env) => env switch
    {
        KsefEnvironment.TE => "TE",
        KsefEnvironment.TR => "TR",
        _ => "PRD"
    };
}

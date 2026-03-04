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
public sealed class KsefSyncSettingsController : ControllerBase
{
    private readonly string _connString;
    private readonly IConfiguration _cfg;

    public KsefSyncSettingsController(IConfiguration configuration)
    {
        _cfg = configuration;
        _connString = configuration.GetConnectionString("Main")
            ?? throw new InvalidOperationException("Brak connection string 'Main'.");
    }

    // GET /ksef/sync/settings?env=PRD
    [HttpGet("settings")]
    [RequirePermission("ksef.sync.manage")]
    public async Task<IActionResult> GetSettings(
        [FromQuery] KsefEnvironment env,
        CancellationToken ct)
    {
        var contextNip = GetContextNip(env);

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
select enabled, interval_minutes, updated_at_utc
from app_core.ksef_auto_sync_settings
where environment = @env and context_nip = @nip
limit 1;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", EnvToText(env));
        cmd.Parameters.AddWithValue("nip", contextNip);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return Ok(new
            {
                enabled = false,
                intervalMinutes = 60,
                contextNip = contextNip,
                environment = EnvToText(env)
            });
        }

        return Ok(new
        {
            enabled = reader.GetBoolean(0),
            intervalMinutes = reader.GetInt32(1),
            updatedAtUtc = reader.GetDateTime(2),
            contextNip = contextNip,
            environment = EnvToText(env)
        });
    }

    // PUT /ksef/sync/settings?env=PRD
    [HttpPut("settings")]
    [RequirePermission("ksef.sync.manage")]
    public async Task<IActionResult> UpdateSettings(
        [FromQuery] KsefEnvironment env,
        [FromBody] UpdateSyncSettingsRequest req,
        CancellationToken ct)
    {
        if (req.IntervalMinutes < 5)
            return BadRequest("intervalMinutes musi być >= 5");

        var contextNip = GetContextNip(env);
        var userId = GetUserId();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
insert into app_core.ksef_auto_sync_settings
(environment, context_nip, enabled, interval_minutes, updated_by_user_id, updated_at_utc)
values
(@env, @nip, @enabled, @interval, @uid, now())
on conflict (environment, context_nip)
do update set
enabled = excluded.enabled,
interval_minutes = excluded.interval_minutes,
updated_by_user_id = excluded.updated_by_user_id,
updated_at_utc = now();";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", EnvToText(env));
        cmd.Parameters.AddWithValue("nip", contextNip);
        cmd.Parameters.AddWithValue("enabled", req.Enabled);
        cmd.Parameters.AddWithValue("interval", req.IntervalMinutes);
        cmd.Parameters.AddWithValue("uid", userId);

        await cmd.ExecuteNonQueryAsync(ct);

        return NoContent();
    }

    private string GetContextNip(KsefEnvironment env)
    {
        var envKey = EnvToText(env); // "TE"/"TR"/"PRD"
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

public sealed class UpdateSyncSettingsRequest
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; }
}

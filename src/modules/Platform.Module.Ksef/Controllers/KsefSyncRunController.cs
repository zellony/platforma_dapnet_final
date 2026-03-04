using System;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/sync")]
[Authorize]
public sealed class KsefSyncRunController : ControllerBase
{
    private readonly IKsefHttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly string _connString;

    public KsefSyncRunController(IKsefHttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
        _connString = cfg.GetConnectionString("Main")
            ?? throw new InvalidOperationException("Brak connection string 'Main'.");
    }

    // ============================================================
    // AUTO
    // POST /ksef/sync/run?env=TE
    // ============================================================
    [HttpPost("run")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> Run(
        [FromQuery] KsefEnvironment env,
        [FromQuery] int pageSize = 250,
        [FromQuery] int maxPages = 1,
        CancellationToken ct = default)
    {
        if (pageSize < 10 || pageSize > 250)
            return BadRequest("pageSize musi być w zakresie 10..250.");

        if (maxPages < 1 || maxPages > 100)
            return BadRequest("maxPages musi być w zakresie 1..100.");

        var userId = GetUserId();
        var envText = EnvToText(env);
        var contextNip = GetContextNip(env);

        // 1) lock (30 min)
        var lockAcquired = await TryAcquireLockAsync(envText, contextNip, userId, lockMinutes: 30, ct);
        if (!lockAcquired)
            return Conflict("Synchronizacja już trwa dla tego (env + ContextNip).");

        try
        {
            // 2) zakres AUTO = HWM lub 89 dni
            var lastHwm = await GetLastHwmAsync(envText, contextNip, ct);

            var toUtc = DateTimeOffset.UtcNow;
            var maxWindowFromUtc = toUtc.AddDays(-89);

            var fromUtc = lastHwm ?? maxWindowFromUtc;
            if (fromUtc < maxWindowFromUtc)
                fromUtc = maxWindowFromUtc;

            // 3) fetch + upsert
            var result = await FetchAndUpsertAsync(
                userId: userId,
                env: env,
                envText: envText,
                contextNip: contextNip,
                fromUtc: fromUtc,
                toUtc: toUtc,
                restrictToPermanentStorageHwmDate: true,
                pageSize: pageSize,
                maxPages: maxPages,
                ct: ct);

            // 4) sync_state (AUTO)
            await UpsertSyncStateAsync(
                envText,
                contextNip,
                lastHwmDateIso: result.permanentStorageHwmDate,
                lastAttemptUtc: DateTimeOffset.UtcNow,
                lastSuccessUtc: DateTimeOffset.UtcNow,
                lastError: null,
                ct);

            return Ok(new
            {
                environment = envText,
                contextNip,
                mode = "AUTO",
                fromUtc = fromUtc.ToString("O"),
                toUtc = toUtc.ToString("O"),
                lastHwmBefore = lastHwm?.ToString("O"),
                permanentStorageHwmDate = result.permanentStorageHwmDate,
                result.pagesFetched,
                result.invoicesFetched,
                result.invoicesProcessed,
                result.inserted,
                result.updated,
                result.skippedNoKsefNumber,
                result.isTruncated
            });
        }
        catch (Exception ex)
        {
            await UpsertSyncStateAsync(
                envText,
                contextNip,
                lastHwmDateIso: null,
                lastAttemptUtc: DateTimeOffset.UtcNow,
                lastSuccessUtc: null,
                lastError: ex.Message,
                ct);

            throw;
        }
    }

    // ============================================================
    // MANUAL (DATE-ONLY, czas lokalny Europe/Warsaw)
    // POST /ksef/sync/manual?env=TE&from=2026-02-01&to=2026-02-17
    // ============================================================
    [HttpPost("manual")]
    [RequirePermission("ksef.import.manual")] // <-- ZMIANA: osobny permission
    public async Task<IActionResult> Manual(
        [FromQuery] KsefEnvironment env,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int pageSize = 250,
        [FromQuery] int maxPages = 1,
        CancellationToken ct = default)
    {
        if (pageSize < 10 || pageSize > 250)
            return BadRequest("pageSize musi być w zakresie 10..250.");

        if (maxPages < 1 || maxPages > 100)
            return BadRequest("maxPages musi być w zakresie 1..100.");

        if (to < from)
            return BadRequest("to musi być >= from.");

        var days = (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).TotalDays;
        if (days > 89)
            return BadRequest("Zakres dat nie może przekraczać 89 dni (limit KSeF ≤ 3 miesiące).");

        var userId = GetUserId();
        var envText = EnvToText(env);
        var contextNip = GetContextNip(env);

        var tz = GetWarsawTimeZone();
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        var fromLocalDt = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Unspecified);

        // "dzisiaj" w lokalnym czasie
        var todayLocal = DateOnly.FromDateTime(nowLocal.DateTime);

        // jeżeli user podał przyszłą datę (dzień) -> 400
        if (to > todayLocal)
            return BadRequest("to (czas lokalny) nie może być w przyszłości.");

        // jeżeli to = dzisiaj -> bierzemy aktualny czas (a nie 23:59:59)
        var toLocalDt = to == todayLocal
            ? DateTime.SpecifyKind(nowLocal.DateTime, DateTimeKind.Unspecified)
            : new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Unspecified);

        var fromUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(fromLocalDt, tz), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(toLocalDt, tz), TimeSpan.Zero);

        var result = await FetchAndUpsertAsync(
            userId: userId,
            env: env,
            envText: envText,
            contextNip: contextNip,
            fromUtc: fromUtc,
            toUtc: toUtc,
            restrictToPermanentStorageHwmDate: false,
            pageSize: pageSize,
            maxPages: maxPages,
            ct: ct);

        var fromLocalOut = TimeZoneInfo.ConvertTime(fromUtc, tz);
        var toLocalOut = TimeZoneInfo.ConvertTime(toUtc, tz);

        return Ok(new
        {
            environment = envText,
            contextNip,
            mode = "MANUAL_LOCAL_DATE",
            from = from.ToString("yyyy-MM-dd"),
            to = to.ToString("yyyy-MM-dd"),
            fromLocal = fromLocalOut.ToString("O"),
            toLocal = toLocalOut.ToString("O"),
            fromUtc = fromUtc.ToString("O"),
            toUtc = toUtc.ToString("O"),
            permanentStorageHwmDate = (string?)null,
            result.pagesFetched,
            result.invoicesFetched,
            result.invoicesProcessed,
            result.inserted,
            result.updated,
            result.skippedNoKsefNumber,
            result.isTruncated
        });
    }

    // ========================= core fetch + upsert =========================

    private async Task<(int pagesFetched, int invoicesFetched, int invoicesProcessed, int inserted, int updated, int skippedNoKsefNumber, bool isTruncated, string? permanentStorageHwmDate)>
        FetchAndUpsertAsync(
            Guid userId,
            KsefEnvironment env,
            string envText,
            string contextNip,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            bool restrictToPermanentStorageHwmDate,
            int pageSize,
            int maxPages,
            CancellationToken ct)
    {
        var pagesFetched = 0;
        var totalFetched = 0;
        var totalProcessed = 0;
        var inserted = 0;
        var updated = 0;
        var skippedNoKsefNumber = 0;
        var isTruncated = false;
        string? newHwm = null;

        var offset = 0;

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var upsertCmd = BuildUpsertInvoiceCommand(conn, tx);

        for (var page = 1; page <= maxPages; page++)
        {
            var mfBody = new
            {
                subjectType = "Subject2",
                dateRange = new
                {
                    dateType = "PermanentStorage",
                    from = fromUtc.UtcDateTime,
                    to = toUtc.UtcDateTime,
                    restrictToPermanentStorageHwmDate = restrictToPermanentStorageHwmDate
                }
            };

            var bodyJson = JsonSerializer.Serialize(mfBody, JsonOpts);
            var path = $"invoices/query/metadata?sortOrder=Asc&pageOffset={offset}&pageSize={pageSize}";

            var pageJson = await _http.PostWithUserTokenAsync(userId, env, path, bodyJson, "application/json", ct);
            pagesFetched++;

            using var doc = JsonDocument.Parse(pageJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("isTruncated", out var it))
                isTruncated = it.GetBoolean();

            if (root.TryGetProperty("permanentStorageHwmDate", out var hwm))
                newHwm = hwm.GetString();

            var countOnPage = 0;

            if (root.TryGetProperty("invoices", out var inv) && inv.ValueKind == JsonValueKind.Array)
            {
                foreach (var invoiceEl in inv.EnumerateArray())
                {
                    totalFetched++;
                    countOnPage++;

                    var ksefNumber =
                        TryGetString(invoiceEl, "ksefNumber")
                        ?? TryGetString(invoiceEl, "ksef_number")
                        ?? TryGetString(invoiceEl, "ksefReferenceNumber")
                        ?? TryGetString(invoiceEl, "ksef_reference_number");

                    if (string.IsNullOrWhiteSpace(ksefNumber))
                    {
                        skippedNoKsefNumber++;
                        continue;
                    }

                    var rawJson = invoiceEl.GetRawText();

                    upsertCmd.Parameters["environment"].Value = envText;
                    upsertCmd.Parameters["context_nip"].Value = contextNip;
                    upsertCmd.Parameters["ksef_number"].Value = ksefNumber.Trim();

                    upsertCmd.Parameters["invoice_number"].Value = (object?)TryGetString(invoiceEl, "invoiceNumber") ?? DBNull.Value;
                    upsertCmd.Parameters["issue_date"].Value = (object?)TryGetDateOnly(invoiceEl, "issueDate") ?? DBNull.Value;
                    upsertCmd.Parameters["invoicing_date"].Value = (object?)TryGetDateTimeOffset(invoiceEl, "invoicingDate") ?? DBNull.Value;
                    upsertCmd.Parameters["acquisition_date"].Value = (object?)TryGetDateTimeOffset(invoiceEl, "acquisitionDate") ?? DBNull.Value;
                    upsertCmd.Parameters["permanent_storage_date"].Value = (object?)TryGetDateTimeOffset(invoiceEl, "permanentStorageDate") ?? DBNull.Value;
                    upsertCmd.Parameters["seller_nip"].Value =
                               (object?)TryGetStringAtPath(invoiceEl, "seller", "nip") ?? DBNull.Value;
                    upsertCmd.Parameters["seller_name"].Value =
                               (object?)TryGetStringAtPath(invoiceEl, "seller", "name") ?? DBNull.Value;
                    upsertCmd.Parameters["buyer_identifier_type"].Value =
                               (object?)TryGetStringAtPath(invoiceEl, "buyer", "identifier", "type") ?? DBNull.Value;
                    upsertCmd.Parameters["buyer_identifier_value"].Value =
                               (object?)TryGetStringAtPath(invoiceEl, "buyer", "identifier", "value") ?? DBNull.Value;
                    upsertCmd.Parameters["buyer_name"].Value =
                               (object?)TryGetStringAtPath(invoiceEl, "buyer", "name") ?? DBNull.Value;
                    upsertCmd.Parameters["net_amount"].Value = (object?)TryGetDecimal(invoiceEl, "netAmount") ?? DBNull.Value;
                    upsertCmd.Parameters["gross_amount"].Value = (object?)TryGetDecimal(invoiceEl, "grossAmount") ?? DBNull.Value;
                    upsertCmd.Parameters["vat_amount"].Value = (object?)TryGetDecimal(invoiceEl, "vatAmount") ?? DBNull.Value;
                    upsertCmd.Parameters["currency"].Value = (object?)TryGetString(invoiceEl, "currency") ?? DBNull.Value;
                    upsertCmd.Parameters["invoicing_mode"].Value = (object?)TryGetString(invoiceEl, "invoicingMode") ?? DBNull.Value;
                    upsertCmd.Parameters["invoice_type"].Value = (object?)TryGetString(invoiceEl, "invoiceType") ?? DBNull.Value;
                    upsertCmd.Parameters["has_attachment"].Value = (object?)TryGetBool(invoiceEl, "hasAttachment") ?? DBNull.Value;
                    upsertCmd.Parameters["invoice_hash"].Value = (object?)TryGetString(invoiceEl, "invoiceHash") ?? DBNull.Value;

                    upsertCmd.Parameters["raw_json"].Value = rawJson;

                    var wasInserted = (bool)(await upsertCmd.ExecuteScalarAsync(ct) ?? false);
                    totalProcessed++;

                    if (wasInserted) inserted++;
                    else updated++;
                }
            }

            if (countOnPage < pageSize)
                break;

            offset += pageSize;
        }

        await tx.CommitAsync(ct);

        return (pagesFetched, totalFetched, totalProcessed, inserted, updated, skippedNoKsefNumber, isTruncated, newHwm);
    }

    // ========================= DB: upsert invoices =========================

    private static NpgsqlCommand BuildUpsertInvoiceCommand(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        const string sql = @"
insert into app_core.ksef_purchase_invoices
(
    environment,
    context_nip,
    ksef_number,
    invoice_number,
    issue_date,
    invoicing_date,
    acquisition_date,
    permanent_storage_date,
    seller_nip,
    seller_name,
    buyer_identifier_type,
    buyer_identifier_value,
    buyer_name,
    net_amount,
    gross_amount,
    vat_amount,
    currency,
    invoicing_mode,
    invoice_type,
    has_attachment,
    invoice_hash,
    raw_json,
    updated_at_utc
)
values
(
    @environment,
    @context_nip,
    @ksef_number,
    @invoice_number,
    @issue_date,
    @invoicing_date,
    @acquisition_date,
    @permanent_storage_date,
    @seller_nip,
    @seller_name,
    @buyer_identifier_type,
    @buyer_identifier_value,
    @buyer_name,
    @net_amount,
    @gross_amount,
    @vat_amount,
    @currency,
    @invoicing_mode,
    @invoice_type,
    @has_attachment,
    @invoice_hash,
    @raw_json::jsonb,
    now()
)
on conflict (environment, ksef_number)
do update set
    context_nip = excluded.context_nip,
    invoice_number = excluded.invoice_number,
    issue_date = excluded.issue_date,
    invoicing_date = excluded.invoicing_date,
    acquisition_date = excluded.acquisition_date,
    permanent_storage_date = excluded.permanent_storage_date,
    seller_nip = excluded.seller_nip,
    seller_name = excluded.seller_name,
    buyer_identifier_type = excluded.buyer_identifier_type,
    buyer_identifier_value = excluded.buyer_identifier_value,
    buyer_name = excluded.buyer_name,
    net_amount = excluded.net_amount,
    gross_amount = excluded.gross_amount,
    vat_amount = excluded.vat_amount,
    currency = excluded.currency,
    invoicing_mode = excluded.invoicing_mode,
    invoice_type = excluded.invoice_type,
    has_attachment = excluded.has_attachment,
    invoice_hash = excluded.invoice_hash,
    raw_json = excluded.raw_json,
    updated_at_utc = now()
returning (xmax = 0);";

        var cmd = new NpgsqlCommand(sql, conn, tx);

        cmd.Parameters.Add(new NpgsqlParameter("environment", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("context_nip", NpgsqlDbType.Varchar));
        cmd.Parameters.Add(new NpgsqlParameter("ksef_number", NpgsqlDbType.Text));

        cmd.Parameters.Add(new NpgsqlParameter("invoice_number", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("issue_date", NpgsqlDbType.Date));
        cmd.Parameters.Add(new NpgsqlParameter("invoicing_date", NpgsqlDbType.TimestampTz));
        cmd.Parameters.Add(new NpgsqlParameter("acquisition_date", NpgsqlDbType.TimestampTz));
        cmd.Parameters.Add(new NpgsqlParameter("permanent_storage_date", NpgsqlDbType.TimestampTz));

        cmd.Parameters.Add(new NpgsqlParameter("seller_nip", NpgsqlDbType.Varchar));
        cmd.Parameters.Add(new NpgsqlParameter("seller_name", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("buyer_identifier_type", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("buyer_identifier_value", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("buyer_name", NpgsqlDbType.Text));

        cmd.Parameters.Add(new NpgsqlParameter("net_amount", NpgsqlDbType.Numeric));
        cmd.Parameters.Add(new NpgsqlParameter("gross_amount", NpgsqlDbType.Numeric));
        cmd.Parameters.Add(new NpgsqlParameter("vat_amount", NpgsqlDbType.Numeric));
        cmd.Parameters.Add(new NpgsqlParameter("currency", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("invoicing_mode", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("invoice_type", NpgsqlDbType.Text));
        cmd.Parameters.Add(new NpgsqlParameter("has_attachment", NpgsqlDbType.Boolean));
        cmd.Parameters.Add(new NpgsqlParameter("invoice_hash", NpgsqlDbType.Text));

        cmd.Parameters.Add(new NpgsqlParameter("raw_json", NpgsqlDbType.Jsonb));

        cmd.Prepare();
        return cmd;
    }

    // ========================= DB: lock + state =========================

    private async Task<bool> TryAcquireLockAsync(string env, string nip, Guid userId, int lockMinutes, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
insert into app_core.ksef_sync_lock (environment, context_nip, locked_until_utc, locked_by_user_id)
values (@env, @nip, now() + (@mins || ' minutes')::interval, @uid)
on conflict (environment, context_nip)
do update set
locked_until_utc = excluded.locked_until_utc,
locked_by_user_id = excluded.locked_by_user_id
where app_core.ksef_sync_lock.locked_until_utc <= now();";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", env);
        cmd.Parameters.AddWithValue("nip", nip);
        cmd.Parameters.AddWithValue("mins", lockMinutes);
        cmd.Parameters.AddWithValue("uid", userId);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    private async Task<DateTimeOffset?> GetLastHwmAsync(string env, string nip, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
select last_permanent_storage_hwm_date
from app_core.ksef_sync_state
where environment = @env and context_nip = @nip
limit 1;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", env);
        cmd.Parameters.AddWithValue("nip", nip);

        var obj = await cmd.ExecuteScalarAsync(ct);
        if (obj is null || obj is DBNull)
            return null;

        if (obj is DateTimeOffset dto)
            return dto;

        if (obj is DateTime dt)
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        return null;
    }

    private async Task UpsertSyncStateAsync(
        string env,
        string nip,
        string? lastHwmDateIso,
        DateTimeOffset? lastAttemptUtc,
        DateTimeOffset? lastSuccessUtc,
        string? lastError,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
insert into app_core.ksef_sync_state
(environment, context_nip, last_permanent_storage_hwm_date, last_attempt_at_utc, last_success_at_utc, last_error, updated_at_utc)
values
(@env, @nip, @hwm, @attempt, @success, @err, now())
on conflict (environment, context_nip)
do update set
last_permanent_storage_hwm_date = coalesce(excluded.last_permanent_storage_hwm_date, app_core.ksef_sync_state.last_permanent_storage_hwm_date),
last_attempt_at_utc = excluded.last_attempt_at_utc,
last_success_at_utc = coalesce(excluded.last_success_at_utc, app_core.ksef_sync_state.last_success_at_utc),
last_error = excluded.last_error,
updated_at_utc = now();";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("env", env);
        cmd.Parameters.AddWithValue("nip", nip);

        if (string.IsNullOrWhiteSpace(lastHwmDateIso))
            cmd.Parameters.AddWithValue("hwm", DBNull.Value);
        else
            cmd.Parameters.AddWithValue("hwm", DateTimeOffset.Parse(lastHwmDateIso));

        cmd.Parameters.AddWithValue("attempt", (object?)lastAttemptUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("success", (object?)lastSuccessUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("err", (object?)lastError ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ========================= JSON helpers =========================

    private static string? TryGetString(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var p)) return null;

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static DateOnly? TryGetDateOnly(JsonElement el, string prop)
    {
        var s = TryGetString(el, prop);
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateOnly.TryParse(s, out var d)) return d;
        if (DateTime.TryParse(s, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement el, string prop)
    {
        var s = TryGetString(el, prop);
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateTimeOffset.TryParse(s, out var dto)) return dto;
        if (DateTime.TryParse(s, out var dt)) return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        return null;
    }

    private static decimal? TryGetDecimal(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var p)) return null;

        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var ds)) return ds;

        return null;
    }

    private static bool? TryGetBool(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var p)) return null;

        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
            _ => null
        };
    }

    private static string? TryGetStringAtPath(JsonElement el, params string[] path)
    {
        var cur = el;

        foreach (var key in path)
        {
            if (cur.ValueKind != JsonValueKind.Object)
                return null;

            if (!cur.TryGetProperty(key, out var next))
                return null;

            cur = next;
        }

        return cur.ValueKind switch
        {
            JsonValueKind.String => cur.GetString(),
            JsonValueKind.Number => cur.GetRawText(),
            _ => null
        };
    }


    // ========================= helpers =========================

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

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string EnvToText(KsefEnvironment env) => env switch
    {
        KsefEnvironment.TE => "TE",
        KsefEnvironment.TR => "TR",
        _ => "PRD"
    };

    private static TimeZoneInfo GetWarsawTimeZone()
    {
        // Linux / Docker
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        }
        catch
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
    }
}

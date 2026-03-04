using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("app/ksef/purchase-invoices")]
[Authorize]
public sealed class KsefPurchaseInvoicesController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly string _connString;

    public KsefPurchaseInvoicesController(IConfiguration cfg)
    {
        _cfg = cfg;
        _connString = cfg.GetConnectionString("Main")
            ?? throw new InvalidOperationException("Brak connection string 'Main'.");
    }

    // ============================================================
    // GET /app/ksef/purchase-invoices?env=TE&page=1&pageSize=50&...
    // ============================================================
    [HttpGet]
    [RequirePermission("ksef.read")]
    public async Task<IActionResult> List(
        [FromQuery] KsefEnvironment env,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] DateOnly? issueDateFrom = null,
        [FromQuery] DateOnly? issueDateTo = null,
        [FromQuery] string? sellerNip = null,
        [FromQuery] bool? hasAttachment = null,
        [FromQuery] string? sort = "permanentStorageDate",
        [FromQuery] string? dir = "desc",
        CancellationToken ct = default)
    {
        if (page < 1)
            return BadRequest("page musi być >= 1.");

        if (pageSize < 10 || pageSize > 250)
            return BadRequest("pageSize musi być w zakresie 10..250.");

        if (issueDateFrom.HasValue && issueDateTo.HasValue && issueDateTo.Value < issueDateFrom.Value)
            return BadRequest("issueDateTo musi być >= issueDateFrom.");

        // whitelist sort
        var sortKey = (sort ?? "permanentStorageDate").Trim();
        var dirKey = (dir ?? "desc").Trim().ToLowerInvariant();

        if (dirKey is not ("asc" or "desc"))
            return BadRequest("dir musi być 'asc' albo 'desc'.");

        var orderBySql = BuildOrderBy(sortKey, dirKey);

        var envText = EnvToText(env);
        var contextNip = GetContextNip(env);

        var offset = (page - 1) * pageSize;

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        // 1) COUNT(*)
        var total = await CountAsync(conn, envText, contextNip, q, issueDateFrom, issueDateTo, sellerNip, hasAttachment, ct);

        // 2) LIST
        var items = await ListAsync(conn, envText, contextNip, q, issueDateFrom, issueDateTo, sellerNip, hasAttachment, pageSize, offset, orderBySql, ct);

        return Ok(new
        {
            environment = envText,
            contextNip,
            page,
            pageSize,
            total,
            items
        });
    }

    // ========================= DB =========================

    private static async Task<long> CountAsync(
        NpgsqlConnection conn,
        string env,
        string contextNip,
        string? q,
        DateOnly? issueDateFrom,
        DateOnly? issueDateTo,
        string? sellerNip,
        bool? hasAttachment,
        CancellationToken ct)
    {
        var (whereSql, binder) = BuildWhere(env, contextNip, q, issueDateFrom, issueDateTo, sellerNip, hasAttachment);

        var sql = $@"
select count(*)
from app_core.ksef_purchase_invoices
{whereSql};";

        await using var cmd = new NpgsqlCommand(sql, conn);
        binder(cmd);

        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj is null or DBNull ? 0 : Convert.ToInt64(obj);
    }

    private static async Task<List<object>> ListAsync(
        NpgsqlConnection conn,
        string env,
        string contextNip,
        string? q,
        DateOnly? issueDateFrom,
        DateOnly? issueDateTo,
        string? sellerNip,
        bool? hasAttachment,
        int pageSize,
        int offset,
        string orderBySql,
        CancellationToken ct)
    {
        var (whereSql, binder) = BuildWhere(env, contextNip, q, issueDateFrom, issueDateTo, sellerNip, hasAttachment);

        var sql = $@"
select
    id,
    environment,
    context_nip,
    ksef_number,
    invoice_number,
    issue_date,
    permanent_storage_date,
    seller_nip,
    seller_name,
    buyer_identifier_type,
    buyer_identifier_value,
    buyer_name,
    net_amount,
    vat_amount,
    gross_amount,
    currency,
    has_attachment,
    invoice_hash,
    updated_at_utc
from app_core.ksef_purchase_invoices
{whereSql}
{orderBySql}
limit @pageSize
offset @offset;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        binder(cmd);

        cmd.Parameters.Add(new NpgsqlParameter("pageSize", NpgsqlDbType.Integer) { Value = pageSize });
        cmd.Parameters.Add(new NpgsqlParameter("offset", NpgsqlDbType.Integer) { Value = offset });

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<object>(capacity: Math.Min(pageSize, 250));

        while (await reader.ReadAsync(ct))
        {
            list.Add(new
            {
                id = reader.GetInt64(0),
                environment = reader.GetString(1),
                contextNip = reader.GetString(2),
                ksefNumber = reader.GetString(3),
                invoiceNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                issueDate = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5).ToString("yyyy-MM-dd"),
                permanentStorageDate = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6).ToString("O"),
                sellerNip = reader.IsDBNull(7) ? null : reader.GetString(7),
                sellerName = reader.IsDBNull(8) ? null : reader.GetString(8),
                buyerIdentifierType = reader.IsDBNull(9) ? null : reader.GetString(9),
                buyerIdentifierValue = reader.IsDBNull(10) ? null : reader.GetString(10),
                buyerName = reader.IsDBNull(11) ? null : reader.GetString(11),
                netAmount = reader.IsDBNull(12) ? (decimal?)null : reader.GetDecimal(12),
                vatAmount = reader.IsDBNull(13) ? (decimal?)null : reader.GetDecimal(13),
                grossAmount = reader.IsDBNull(14) ? (decimal?)null : reader.GetDecimal(14),
                hasAttachment = reader.IsDBNull(16) ? (bool?)null : reader.GetBoolean(16),
                invoiceHash = reader.IsDBNull(17) ? null : reader.GetString(17),
                updatedAtUtc = reader.GetFieldValue<DateTimeOffset>(18).ToString("O")
            });
        }

        return list;
    }

    private static (string whereSql, Action<NpgsqlCommand> binder) BuildWhere(
        string env,
        string contextNip,
        string? q,
        DateOnly? issueDateFrom,
        DateOnly? issueDateTo,
        string? sellerNip,
        bool? hasAttachment)
    {
        var clauses = new List<string>(capacity: 8)
        {
            "where environment = @env",
            "and context_nip = @contextNip"
        };

        var qTrim = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (!string.IsNullOrWhiteSpace(qTrim))
        {
            clauses.Add(@"
and (
    ksef_number ilike '%' || @q || '%'
    or invoice_number ilike '%' || @q || '%'
    or seller_name ilike '%' || @q || '%'
    or seller_nip ilike '%' || @q || '%'
)");
        }

        if (issueDateFrom.HasValue)
            clauses.Add("and issue_date >= @issueDateFrom");

        if (issueDateTo.HasValue)
            clauses.Add("and issue_date <= @issueDateTo");

        var nipTrim = string.IsNullOrWhiteSpace(sellerNip) ? null : sellerNip.Trim();
        if (!string.IsNullOrWhiteSpace(nipTrim))
            clauses.Add("and seller_nip = @sellerNip");

        if (hasAttachment.HasValue)
            clauses.Add("and has_attachment = @hasAttachment");

        var whereSql = string.Join('\n', clauses);

        void Binder(NpgsqlCommand cmd)
        {
            cmd.Parameters.Add(new NpgsqlParameter("env", NpgsqlDbType.Text) { Value = env });
            cmd.Parameters.Add(new NpgsqlParameter("contextNip", NpgsqlDbType.Varchar) { Value = contextNip });

            if (!string.IsNullOrWhiteSpace(qTrim))
                cmd.Parameters.Add(new NpgsqlParameter("q", NpgsqlDbType.Text) { Value = qTrim! });

            if (issueDateFrom.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter("issueDateFrom", NpgsqlDbType.Date) { Value = issueDateFrom.Value });

            if (issueDateTo.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter("issueDateTo", NpgsqlDbType.Date) { Value = issueDateTo.Value });

            if (!string.IsNullOrWhiteSpace(nipTrim))
                cmd.Parameters.Add(new NpgsqlParameter("sellerNip", NpgsqlDbType.Varchar) { Value = nipTrim! });

            if (hasAttachment.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter("hasAttachment", NpgsqlDbType.Boolean) { Value = hasAttachment.Value });
        }

        return (whereSql, Binder);
    }

    private static string BuildOrderBy(string sort, string dir)
    {
        var col = sort switch
        {
            "issueDate" => "issue_date",
            "updatedAt" => "updated_at_utc",
            "permanentStorageDate" => "permanent_storage_date",
            _ => "permanent_storage_date"
        };

        return $@"
order by
    {col} {dir} nulls last,
    issue_date desc nulls last,
    id desc";
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

    private static string EnvToText(KsefEnvironment env) => env switch
    {
        KsefEnvironment.TE => "TE",
        KsefEnvironment.TR => "TR",
        _ => "PRD"
    };
}

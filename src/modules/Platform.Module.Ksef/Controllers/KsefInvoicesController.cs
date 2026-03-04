using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Kernel.Core.RBAC;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;
using Platform.Api.Modules.KSeF.Invoices.Purchase.Models;

namespace Platform.Api.Modules.KSeF.Controllers;

[ApiController]
[Route("ksef/invoices")]
[Authorize]
public sealed class KsefInvoicesController : ControllerBase
{
    private readonly IKsefHttpClient _http;

    public KsefInvoicesController(IKsefHttpClient http)
    {
        _http = http;
    }

    // A3.7.1: TE ma limit 20 req/h -> blokujemy paginację > 1 strona, bo zabije limit.
    // Dodatkowo: jeśli KSeF zwróci 429, oddajemy 429 z body KSeF zamiast exception page.
    [HttpPost("purchase/query")]
    [RequirePermission("ksef.import")]
    public async Task<IActionResult> QueryPurchaseInvoices(
        [FromBody] KsefPurchaseQueryRequest req,
        [FromQuery] int pageOffset = 0,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortOrder = "Asc",
        [FromQuery] int maxPages = 1,
        CancellationToken ct = default)
    {
        if (req.DateTo < req.DateFrom)
            return BadRequest("DateTo musi być >= DateFrom.");

        if (pageOffset < 0)
            return BadRequest("pageOffset musi być >= 0.");

        if (pageSize < 10 || pageSize > 250)
            return BadRequest("pageSize musi być w zakresie 10..250.");

        if (maxPages < 1 || maxPages > 500)
            return BadRequest("maxPages musi być w zakresie 1..500.");

        if (!string.Equals(sortOrder, "Asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortOrder, "Desc", StringComparison.OrdinalIgnoreCase))
            return BadRequest("sortOrder musi być 'Asc' albo 'Desc'.");

        // TE: limit 20 req/h — realnie nie opłaca się robić pętli.
        // W TE testujemy pojedynczą stronę (ustaw pageSize=250, maxPages=1).
        if (req.Env == KsefEnvironment.TE && maxPages > 1)
            return BadRequest("KSeF TE ma limit 20 żądań/h. Paginacja (maxPages>1) wyłączona w TE. Użyj pageSize=250 i maxPages=1.");

        var userId = GetUserId();
        var sellerNip = MapSingleSellerNipOrNull(req);

        var mfBody = new
        {
            subjectType = "Subject2",
            dateRange = new
            {
                dateType = "PermanentStorage",
                from = EnsureUtc(req.DateFrom),
                to = EnsureUtc(req.DateTo),
                restrictToPermanentStorageHwmDate = true
            },
            sellerNip = sellerNip
        };

        var bodyJson = JsonSerializer.Serialize(mfBody, JsonOpts);

        var allInvoices = new List<JsonElement>();
        string? permanentStorageHwmDate = null;
        bool isTruncated = false;

        var currentOffset = pageOffset;
        var pagesFetchedActual = 0;

        for (var page = 1; page <= maxPages; page++)
        {
            var path = $"invoices/query/metadata?sortOrder={sortOrder}&pageOffset={currentOffset}&pageSize={pageSize}";

            string pageJson;
            try
            {
                pageJson = await _http.PostWithUserTokenAsync(
                    userId,
                    req.Env,
                    path,
                    bodyJson,
                    contentType: "application/json",
                    ct);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains(" 429 ") || ex.Message.Contains("429 Too Many Requests"))
            {
                // nasz KsefHttpClient wrzuca body po '\n'
                var idx = ex.Message.IndexOf('\n');
                var body = idx >= 0 ? ex.Message[(idx + 1)..] : null;

                if (!string.IsNullOrWhiteSpace(body))
                    return new ContentResult { StatusCode = 429, ContentType = "application/json", Content = body };

                return StatusCode(429, new { error = ex.Message });
            }

            pagesFetchedActual++;

            using var doc = JsonDocument.Parse(pageJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("isTruncated", out var it))
                isTruncated = it.GetBoolean();

            if (root.TryGetProperty("permanentStorageHwmDate", out var hwm))
                permanentStorageHwmDate = hwm.GetString();

            if (root.TryGetProperty("invoices", out var inv) && inv.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in inv.EnumerateArray())
                    allInvoices.Add(item.Clone());
            }

            // Jeśli ktoś jednak ustawi maxPages>1 (np. TR/PRD), przesuwamy offset.
            currentOffset += pageSize;
        }

        return Ok(new
        {
            requestEcho = new
            {
                req.Env,
                req.DateFrom,
                req.DateTo,
                sellerNip,
                pageOffsetStart = pageOffset,
                pageSize,
                sortOrder,
                maxPages
            },
            pagesFetchedActual,
            isTruncated,
            permanentStorageHwmDate,
            invoicesCount = allInvoices.Count,
            invoices = allInvoices
        });
    }

    private static string? MapSingleSellerNipOrNull(KsefPurchaseQueryRequest req)
    {
        var nips = req.Filters?.Nips;
        if (nips == null || nips.Count == 0)
            return null;

        var cleaned = nips
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (cleaned.Count == 0)
            return null;

        if (cleaned.Count > 1)
            return null;

        return cleaned[0];
    }

    private static DateTime EnsureUtc(DateTime dt)
        => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
            throw new InvalidOperationException("Brak claim 'sub' w JWT.");
        return Guid.Parse(sub);
    }
}

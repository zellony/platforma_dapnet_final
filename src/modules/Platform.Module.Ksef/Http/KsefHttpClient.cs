using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Options;
using Platform.Api.Modules.KSeF.Services;

namespace Platform.Api.Modules.KSeF.Http;

public interface IKsefHttpClient
{
    Task<string> PostAsync(KsefEnvironment env, string path, string? bearerToken, string? body, string contentType, CancellationToken ct);
    Task<string> GetAsync(KsefEnvironment env, string path, string? bearerToken, CancellationToken ct);

    // token z DB + auto-refresh (dla requestów biznesowych) — GET
    Task<string> GetWithUserTokenAsync(Guid userId, KsefEnvironment env, string path, CancellationToken ct);

    // token z DB + auto-refresh — POST
    Task<string> PostWithUserTokenAsync(Guid userId, KsefEnvironment env, string path, string? body, string contentType, CancellationToken ct);
}

public sealed class KsefHttpClient : IKsefHttpClient
{
    private readonly IHttpClientFactory _factory;
    private readonly IOptions<KsefOptions> _options;
    private readonly IKsefCredentialService _cred;
    private readonly string _connString;

    // "Spróbuj ponownie po 2439 sekundach."
    private static readonly Regex RetryAfterSecondsRegex =
        new(@"po\s+(?<sec>\d+)\s+sekund", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public KsefHttpClient(
        IHttpClientFactory factory,
        IOptions<KsefOptions> options,
        IKsefCredentialService cred,
        IConfiguration configuration)
    {
        _factory = factory;
        _options = options;
        _cred = cred;

        _connString = configuration.GetConnectionString("Main")
            ?? throw new InvalidOperationException("Brak connection string 'Main'.");
    }

    public async Task<string> PostAsync(KsefEnvironment env, string path, string? bearerToken, string? body, string contentType, CancellationToken ct)
    {
        using var client = Create(env, bearerToken);

        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body is not null)
            req.Content = new StringContent(body, Encoding.UTF8, contentType);

        using var res = await client.SendAsync(req, ct);
        var txt = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"KSeF POST {path} failed: {(int)res.StatusCode} {res.ReasonPhrase}\n{txt}");

        return txt;
    }

    public async Task<string> GetAsync(KsefEnvironment env, string path, string? bearerToken, CancellationToken ct)
    {
        using var client = Create(env, bearerToken);

        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var res = await client.SendAsync(req, ct);
        var txt = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"KSeF GET {path} failed: {(int)res.StatusCode} {res.ReasonPhrase}\n{txt}");

        return txt;
    }

    // ===== auto-refresh: GET =====
    public async Task<string> GetWithUserTokenAsync(Guid userId, KsefEnvironment env, string path, CancellationToken ct)
    {
        // A.2: per-user rate-limit guard (DB)
        await ThrowIfBlockedAsync(userId, env, ct);

        var row = await _cred.GetAsync(userId, env, ct);

        if (row is null)
            throw new InvalidOperationException("Brak poświadczeń KSeF dla użytkownika w DB.");

        // 1) jeśli access token jest OK -> użyj
        if (!string.IsNullOrWhiteSpace(row.Token) &&
            row.TokenValidTo is not null &&
            row.TokenValidTo > DateTimeOffset.UtcNow)
        {
            try
            {
                return await GetAsync(env, path, row.Token, ct);
            }
            catch (HttpRequestException ex)
            {
                await Handle429IfPresentAsync(userId, env, ex, ct);
                throw;
            }
        }

        // 2) brak/wygaśnięty access -> spróbuj refresh
        if (string.IsNullOrWhiteSpace(row.RefreshToken))
            throw new InvalidOperationException("Brak RefreshToken w DB. Zrób login+redeem.");

        if (row.RefreshTokenValidTo is null || row.RefreshTokenValidTo <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException($"RefreshToken wygasł (RefreshTokenValidTo={row.RefreshTokenValidTo:O}). Zrób login+redeem.");

        var refreshJson = await PostAsync(env, "auth/token/refresh", bearerToken: row.RefreshToken, body: null, contentType: "application/json", ct);

        using (var doc = JsonDocument.Parse(refreshJson))
        {
            var access = doc.RootElement.GetProperty("accessToken");
            var newToken = access.GetProperty("token").GetString()!;
            var newValidUntil = DateTimeOffset.Parse(access.GetProperty("validUntil").GetString()!);

            await _cred.UpsertTokenAsync(userId, env, newToken, newValidUntil, ct);

            // 3) ponów request już z nowym access
            try
            {
                return await GetAsync(env, path, newToken, ct);
            }
            catch (HttpRequestException ex)
            {
                await Handle429IfPresentAsync(userId, env, ex, ct);
                throw;
            }
        }
    }

    // ===== auto-refresh: POST =====
    public async Task<string> PostWithUserTokenAsync(Guid userId, KsefEnvironment env, string path, string? body, string contentType, CancellationToken ct)
    {
        // A.2: per-user rate-limit guard (DB)
        await ThrowIfBlockedAsync(userId, env, ct);

        var row = await _cred.GetAsync(userId, env, ct);

        if (row is null)
            throw new InvalidOperationException("Brak poświadczeń KSeF dla użytkownika w DB.");

        // 1) jeśli access token jest OK -> użyj
        if (!string.IsNullOrWhiteSpace(row.Token) &&
            row.TokenValidTo is not null &&
            row.TokenValidTo > DateTimeOffset.UtcNow)
        {
            try
            {
                return await PostAsync(env, path, row.Token, body, contentType, ct);
            }
            catch (HttpRequestException ex)
            {
                await Handle429IfPresentAsync(userId, env, ex, ct);
                throw;
            }
        }

        // 2) brak/wygaśnięty access -> spróbuj refresh
        if (string.IsNullOrWhiteSpace(row.RefreshToken))
            throw new InvalidOperationException("Brak RefreshToken w DB. Zrób login+redeem.");

        if (row.RefreshTokenValidTo is null || row.RefreshTokenValidTo <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException($"RefreshToken wygasł (RefreshTokenValidTo={row.RefreshTokenValidTo:O}). Zrób login+redeem.");

        var refreshJson = await PostAsync(env, "auth/token/refresh", bearerToken: row.RefreshToken, body: null, contentType: "application/json", ct);

        using (var doc = JsonDocument.Parse(refreshJson))
        {
            var access = doc.RootElement.GetProperty("accessToken");
            var newToken = access.GetProperty("token").GetString()!;
            var newValidUntil = DateTimeOffset.Parse(access.GetProperty("validUntil").GetString()!);

            await _cred.UpsertTokenAsync(userId, env, newToken, newValidUntil, ct);

            // 3) ponów request już z nowym access
            try
            {
                return await PostAsync(env, path, newToken, body, contentType, ct);
            }
            catch (HttpRequestException ex)
            {
                await Handle429IfPresentAsync(userId, env, ex, ct);
                throw;
            }
        }
    }

    // =========================
    // A.2: Rate-limit (DB)
    // =========================

    private async Task ThrowIfBlockedAsync(Guid userId, KsefEnvironment env, CancellationToken ct)
    {
        var blockedUntil = await GetBlockedUntilAsync(userId, env, ct);
        if (blockedUntil is null)
            return;

        var now = DateTimeOffset.UtcNow;
        if (blockedUntil.Value <= now)
            return;

        var seconds = (int)Math.Ceiling((blockedUntil.Value - now).TotalSeconds);
        var blockedUntilIso = blockedUntil.Value.ToString("O");

        var bodyObj = new
        {
            status = new
            {
                code = 429,
                description = "Too Many Requests",
                details = new[]
                {
            $"Zablokowane lokalnie (DB) do {blockedUntilIso}. Spróbuj ponownie po {seconds} sekundach."
        }
            }
        };

        var body = JsonSerializer.Serialize(bodyObj);


        // Ważne: controller już umie łapać 429 po stringu, a dodatkowo to jest czytelne w logach.
        throw new HttpRequestException($"KSeF POST/GET blocked (local): 429 Too Many Requests\n{body}");
    }

    private async Task<DateTimeOffset?> GetBlockedUntilAsync(Guid userId, KsefEnvironment env, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
select blocked_until_utc
from app_core.ksef_rate_limit_state
where user_id = @uid and environment = @env
limit 1;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("env", EnvToText(env));

        var obj = await cmd.ExecuteScalarAsync(ct);
        if (obj is null || obj is DBNull)
            return null;

        if (obj is DateTimeOffset dto)
            return dto;

        if (obj is DateTime dt)
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        throw new InvalidCastException($"Nieobsługiwany typ dla blocked_until_utc: {obj.GetType().FullName}");

    }

    private async Task Handle429IfPresentAsync(Guid userId, KsefEnvironment env, HttpRequestException ex, CancellationToken ct)
    {
        // Nasz PostAsync/GetAsync wrzuca body po '\n'. Jeśli nie ma body, nic nie zrobimy.
        var msg = ex.Message ?? string.Empty;

        // Szybka detekcja 429
        if (!msg.Contains(" 429 ", StringComparison.OrdinalIgnoreCase) &&
            !msg.Contains("429 Too Many Requests", StringComparison.OrdinalIgnoreCase))
            return;

        var idx = msg.IndexOf('\n');
        var body = idx >= 0 ? msg[(idx + 1)..] : null;
        if (string.IsNullOrWhiteSpace(body))
            return;

        var retrySec = TryParseRetryAfterSeconds(body);
        if (retrySec is null || retrySec.Value <= 0)
            retrySec = 60; // fallback ostrożny

        var blockedUntil = DateTimeOffset.UtcNow.AddSeconds(retrySec.Value);

        await UpsertRateLimitAsync(userId, env, blockedUntil, retrySec.Value, body, ct);
    }

    private static int? TryParseRetryAfterSeconds(string ksefBody)
    {
        var m = RetryAfterSecondsRegex.Match(ksefBody);
        if (!m.Success)
            return null;

        if (!int.TryParse(m.Groups["sec"].Value, out var sec))
            return null;

        return sec;
    }

    private async Task UpsertRateLimitAsync(
        Guid userId,
        KsefEnvironment env,
        DateTimeOffset blockedUntilUtc,
        int retryAfterSeconds,
        string details,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
insert into app_core.ksef_rate_limit_state
(user_id, environment, blocked_until_utc, last_429_at_utc, retry_after_seconds, last_details, updated_at_utc)
values
(@uid, @env, @blocked, now(), @retry, @details, now())
on conflict (user_id, environment)
do update set
blocked_until_utc = excluded.blocked_until_utc,
last_429_at_utc = excluded.last_429_at_utc,
retry_after_seconds = excluded.retry_after_seconds,
last_details = excluded.last_details,
updated_at_utc = now();";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("env", EnvToText(env));
        cmd.Parameters.AddWithValue("blocked", blockedUntilUtc);
        cmd.Parameters.AddWithValue("retry", retryAfterSeconds);
        cmd.Parameters.AddWithValue("details", details);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string EnvToText(KsefEnvironment env) => env switch
    {
        KsefEnvironment.TE => "TE",
        KsefEnvironment.TR => "TR",
        _ => "PRD"
    };

    // =========================
    // HttpClient
    // =========================

    private HttpClient Create(KsefEnvironment env, string? bearerToken)
    {
        var client = _factory.CreateClient();

        client.BaseAddress = new Uri(GetBaseUrl(env), UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(bearerToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return client;
    }

    private string GetBaseUrl(KsefEnvironment env)
    {
        var e = _options.Value.Environments;

        var url = env switch
        {
            KsefEnvironment.TE => e.TE.ApiBaseUrl,
            KsefEnvironment.TR => e.TR.ApiBaseUrl,
            _ => e.PRD.ApiBaseUrl
        };

        return url.TrimEnd('/') + "/"; // żeby można było wołać "auth/challenge" itd.
    }
}

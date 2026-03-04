using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Platform.Api.Modules.KSeF.Crypto;
using Platform.Api.Modules.KSeF.Entities;
using Platform.Api.Modules.KSeF.Http;
using Platform.Api.Modules.KSeF.Options;
using Platform.Api.Modules.KSeF.Services;

namespace Platform.Api.Modules.KSeF.Auth;

public sealed class KsefAuthService
{
    private readonly IKsefHttpClient _http;
    private readonly IKsefCredentialService _cred;
    private readonly IOptions<KsefOptions> _opt;

    public KsefAuthService(IKsefHttpClient http, IKsefCredentialService cred, IOptions<KsefOptions> opt)
    {
        _http = http;
        _cred = cred;
        _opt = opt;
    }

    public async Task<string> StartLoginWithCertificateAsync(Guid userId, KsefEnvironment env, CancellationToken ct)
    {
        var challengeJson = await _http.PostAsync(env, "auth/challenge", null, null, "application/json", ct);
        using var chDoc = JsonDocument.Parse(challengeJson);
        var challenge = chDoc.RootElement.GetProperty("challenge").GetString();
        if (string.IsNullOrWhiteSpace(challenge))
            throw new InvalidOperationException("KSeF nie zwrócił pola 'challenge'.");

        var envOpt = GetEnv(env);
        var unsignedXml = KsefAuthTokenRequestXmlBuilder.Build(
            challenge: challenge!,
            env: env,
            contextNip: envOpt.ContextNip,
            subjectIdentifierType: envOpt.SubjectIdentifierType
        );

        var row = await _cred.GetAsync(userId, env, ct)
                  ?? throw new InvalidOperationException("Brak poświadczeń KSeF dla użytkownika.");

        if (row.CertCrt is null || row.CertKey is null)
            throw new InvalidOperationException("Brak certyfikatu/klucza (.crt/.key) w DB dla użytkownika.");

        var pass = await _cred.GetCertificatePasswordAsync(row)
                   ?? throw new InvalidOperationException("Brak hasła do klucza w DB dla użytkownika.");

        using var cert = KsefCertificateLoader.LoadFromCrtKey(row.CertCrt, row.CertKey, pass);
        var signedXml = KsefXadesSigner.Sign(unsignedXml, cert);

        return await _http.PostAsync(env, "auth/xades-signature", null, signedXml, "application/xml", ct);
    }

    // TU jest zmiana: zapis access + refresh
    public async Task SaveTokensFromRedeemAsync(Guid userId, KsefEnvironment env, string redeemJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(redeemJson);
        var root = doc.RootElement;

        var access = root.GetProperty("accessToken");
        var accessToken = access.GetProperty("token").GetString()!;
        var accessValidUntil = DateTimeOffset.Parse(access.GetProperty("validUntil").GetString()!);

        var refresh = root.GetProperty("refreshToken");
        var refreshToken = refresh.GetProperty("token").GetString()!;
        var refreshValidUntil = DateTimeOffset.Parse(refresh.GetProperty("validUntil").GetString()!);

        await _cred.UpsertTokenAndRefreshAsync(
            userId,
            env,
            accessToken,
            accessValidUntil,
            refreshToken,
            refreshValidUntil,
            ct);
    }

    private KsefOptions.KsefSingleEnvOptions GetEnv(KsefEnvironment env)
        => env switch
        {
            KsefEnvironment.TE => _opt.Value.Environments.TE,
            KsefEnvironment.TR => _opt.Value.Environments.TR,
            _ => _opt.Value.Environments.PRD
        };
}

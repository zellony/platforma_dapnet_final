using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Modules.KSeF.Database;
using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Services;

public interface IKsefCredentialService
{
    Task<KsefUserCredential?> GetAsync(Guid userId, KsefEnvironment env, CancellationToken ct = default);

    Task UpsertCertificateAsync(
        Guid userId,
        KsefEnvironment env,
        byte[] certCrt,
        byte[] certKey,
        string certKeyPassword,
        CancellationToken ct = default);

    Task UpsertTokenAsync(
        Guid userId,
        KsefEnvironment env,
        string token,
        DateTimeOffset? validTo,
        CancellationToken ct = default);

    // NOWE: zapis access + refresh z redeem
    Task UpsertTokenAndRefreshAsync(
        Guid userId,
        KsefEnvironment env,
        string accessToken,
        DateTimeOffset? accessValidTo,
        string refreshToken,
        DateTimeOffset? refreshValidTo,
        CancellationToken ct = default);

    Task<string?> GetCertificatePasswordAsync(KsefUserCredential cred);
}

public sealed class KsefCredentialService : IKsefCredentialService
{
    private readonly KsefDbContext _db;
    private readonly IDataProtector _protector;

    public KsefCredentialService(KsefDbContext db, IDataProtectionProvider dp)
    {
        _db = db;
        _protector = dp.CreateProtector("ksef:cert-key-password:v1");
    }

    public Task<KsefUserCredential?> GetAsync(Guid userId, KsefEnvironment env, CancellationToken ct = default)
        => _db.Set<KsefUserCredential>()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Environment == env, ct);

    public async Task UpsertCertificateAsync(
        Guid userId,
        KsefEnvironment env,
        byte[] certCrt,
        byte[] certKey,
        string certKeyPassword,
        CancellationToken ct = default)
    {
        var row = await GetAsync(userId, env, ct);

        if (row is null)
        {
            row = new KsefUserCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Environment = env,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Add(row);
        }

        row.ActiveType = KsefCredentialType.CERT;
        row.CertCrt = certCrt;
        row.CertKey = certKey;
        row.CertKeyPasswordEnc = ProtectToBytes(certKeyPassword);
        row.CertFingerprintSha256 = ComputeCertFingerprintSha256(certCrt);
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // token zostawiamy w DB (fallback), ale nie jest aktywny
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertTokenAsync(
        Guid userId,
        KsefEnvironment env,
        string token,
        DateTimeOffset? validTo,
        CancellationToken ct = default)
    {
        var row = await GetAsync(userId, env, ct);

        if (row is null)
        {
            row = new KsefUserCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Environment = env,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Add(row);
        }

        row.ActiveType = KsefCredentialType.TOKEN;
        row.Token = token;
        row.TokenValidTo = validTo;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertTokenAndRefreshAsync(
        Guid userId,
        KsefEnvironment env,
        string accessToken,
        DateTimeOffset? accessValidTo,
        string refreshToken,
        DateTimeOffset? refreshValidTo,
        CancellationToken ct = default)
    {
        var row = await GetAsync(userId, env, ct);

        if (row is null)
        {
            row = new KsefUserCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Environment = env,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Add(row);
        }

        row.ActiveType = KsefCredentialType.TOKEN;

        row.Token = accessToken;
        row.TokenValidTo = accessValidTo;

        row.RefreshToken = refreshToken;
        row.RefreshTokenValidTo = refreshValidTo;

        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public Task<string?> GetCertificatePasswordAsync(KsefUserCredential cred)
    {
        if (cred.CertKeyPasswordEnc is null || cred.CertKeyPasswordEnc.Length == 0)
            return Task.FromResult<string?>(null);

        var protectedText = Encoding.UTF8.GetString(cred.CertKeyPasswordEnc);
        var plain = _protector.Unprotect(protectedText);
        return Task.FromResult<string?>(plain);
    }

    private byte[] ProtectToBytes(string plain)
    {
        var protectedText = _protector.Protect(plain);
        return Encoding.UTF8.GetBytes(protectedText);
    }

    private static string ComputeCertFingerprintSha256(byte[] certCrt)
    {
        using var cert = LoadPublicOnly(certCrt);
        var hash = SHA256.HashData(cert.RawData);
        return Convert.ToHexString(hash); // uppercase HEX
    }

    private static X509Certificate2 LoadPublicOnly(byte[] certBytes)
    {
        // obsługa PEM/DER (.crt bywa PEM)
        var head = Encoding.ASCII.GetString(certBytes, 0, Math.Min(certBytes.Length, 64));
        if (head.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            var pem = Encoding.UTF8.GetString(certBytes);
            return X509Certificate2.CreateFromPem(pem);
        }

        return new X509Certificate2(certBytes);
    }
}

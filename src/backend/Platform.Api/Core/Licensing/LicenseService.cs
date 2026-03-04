using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Platform.Api.Infrastructure.Database;
using Platform.Kernel.Core.Config.Entities;
using Platform.Kernel.Common;

namespace Platform.Api.Core.Licensing;

public class LicenseService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LicenseService> _logger;

    private const string PUBLIC_KEY_XML = @"<RSAKeyValue><Modulus>tTB5eSNkscdwqQto0ifB/N9RdtYLVfQfqDv0HlL0QNDykzJ6WndiGs7mHchWkbvUfZsOKTNayaqfLKQpY8EVPh9OzFSPHNlnS6NONFMKJhXK21J8pQZngZ4RGHUMk34AOpmwglO/QJLIjetftKttvTUYTuq3BMk5QatXG4cwyFwbt4K1CZ1RNbxAmonR3MSAu4i/dIroBuqpvfy2jLsm4d+dxvWbPF66G2xjwksQkzDc3OaRjUaeTV9uBceeRr22GzeemGrpYS82T1B89QZKIhLL2EtZGmdswuxAFnc3OlCgue+cJ6m7c7et3E80+ZoC3jDNW91viasoLI4hrx0zAQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    private static readonly byte[] _s = { 0x47, 0x72, 0x65, 0x65, 0x6e, 0x49, 0x73, 0x54, 0x68, 0x65, 0x4b, 0x69, 0x6e, 0x67 };
    private static readonly byte[] _salt = { 0x44, 0x41, 0x50, 0x4e, 0x45, 0x54, 0x5f, 0x53, 0x41, 0x4c, 0x54 };

    public LicenseService(AppDbContext db, ILogger<LicenseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LicenseVerificationResult> VerifyAndSaveAsync(string licenseContent)
    {
        try
        {
            var licenseObj = JsonConvert.DeserializeObject<LicenseFileModel>(licenseContent);
            if (licenseObj == null || string.IsNullOrWhiteSpace(licenseObj.Payload) || string.IsNullOrWhiteSpace(licenseObj.Signature))
                return LicenseVerificationResult.InvalidFormat;

            using var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(PUBLIC_KEY_XML);

            var payloadBytes = Encoding.UTF8.GetBytes(licenseObj.Payload);
            var signatureBytes = Convert.FromBase64String(licenseObj.Signature);

            if (!rsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256")!, signatureBytes))
                return LicenseVerificationResult.InvalidSignature;

            string decryptedJson;
            try { decryptedJson = DecryptAes(licenseObj.Payload); }
            catch (Exception ex) { _logger.LogError(ex, "Błąd odszyfrowywania"); return LicenseVerificationResult.InvalidFormat; }

            var data = JsonConvert.DeserializeObject<LicenseDataModel>(decryptedJson);
            if (data == null) return LicenseVerificationResult.InvalidFormat;

            // 1. Sprawdzenie wygaśnięcia licencji (koniec dnia)
            var expirationDate = data.ExpiresAt.Date.AddDays(1).AddTicks(-1);
            if (expirationDate < DateTime.UtcNow) return LicenseVerificationResult.Expired;

            // 2. Sprawdzenie prawa do tej wersji programu (UpdateUntil)
            if (data.UpdateUntil.HasValue)
            {
                // Ustawiamy UpdateUntil na koniec dnia dla korzyści klienta
                var updateLimit = data.UpdateUntil.Value.Date.AddDays(1).AddTicks(-1);
                if (updateLimit < PlatformVersion.ReleaseDate)
                {
                    return LicenseVerificationResult.UpdateExpired;
                }
            }

            var company = await _db.CompanyInfos
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();
            if (company == null) return LicenseVerificationResult.CompanyNotConfigured;
            
            var licenseNip = data.Nip.Replace("-", "").Trim();
            var companyNip = company.Nip.Replace("-", "").Trim();

            if (!string.Equals(licenseNip, companyNip, StringComparison.OrdinalIgnoreCase))
                return LicenseVerificationResult.NipMismatch;

            var existing = await _db.SystemLicenses
                .OrderBy(l => l.CreatedAtUtc)
                .FirstOrDefaultAsync();
            if (existing != null) _db.SystemLicenses.Remove(existing);

            var newLicense = new SystemLicense
            {
                Id = Guid.NewGuid(),
                LicenseBlob = licenseContent,
                CreatedAtUtc = DateTime.UtcNow,
                LastVerifiedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            _db.SystemLicenses.Add(newLicense);
            await _db.SaveChangesAsync();

            return LicenseVerificationResult.Valid;
        }
        catch (Exception ex) { _logger.LogError(ex, "Błąd krytyczny"); return LicenseVerificationResult.InvalidFormat; }
    }

    private string DecryptAes(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);
        using Aes aes = Aes.Create();
        aes.KeySize = 256;
        using var deriveBytes = new Rfc2898DeriveBytes(_s, _salt, 1000, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(32);
        var iv = new byte[16];
        Array.Copy(fullCipher, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }

    public async Task<LicenseStatusDto> GetCurrentStatusAsync()
    {
        var license = await _db.SystemLicenses
            .OrderByDescending(l => l.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (license == null) return new LicenseStatusDto { IsActive = false, Message = "Brak licencji" };

        try
        {
            var licenseObj = JsonConvert.DeserializeObject<LicenseFileModel>(license.LicenseBlob);
            var decryptedJson = DecryptAes(licenseObj!.Payload);
            var data = JsonConvert.DeserializeObject<LicenseDataModel>(decryptedJson);
            
            var expirationDate = data!.ExpiresAt.Date.AddDays(1).AddTicks(-1);
            var daysLeft = (expirationDate - DateTime.UtcNow).TotalDays;
            bool isExpired = daysLeft < 0;

            // Sprawdzenie prawa do aktualizacji
            bool isUpdateExpired = false;
            if (data.UpdateUntil.HasValue)
            {
                var updateLimit = data.UpdateUntil.Value.Date.AddDays(1).AddTicks(-1);
                if (updateLimit < PlatformVersion.ReleaseDate) isUpdateExpired = true;
            }
            
            string message = "Aktywna";
            if (isExpired) message = "Licencja wygasła";
            else if (isUpdateExpired) message = "Brak prawa do tej wersji programu";

            return new LicenseStatusDto 
            { 
                Id = data.Id,
                IsActive = !isExpired && !isUpdateExpired, 
                Message = message, 
                ExpiresAt = expirationDate, 
                Nip = data.Nip,
                DaysLeft = (int)Math.Max(0, Math.Ceiling(daysLeft)),
                Modules = data.Modules,
                Seats = data.Seats,
                UpdateUntil = data.UpdateUntil,
                IsUpdateExpired = isUpdateExpired // ✅ Nowa flaga dla frontendu
            };
        }
        catch
        {
            return new LicenseStatusDto { IsActive = false, Message = "Błąd odczytu licencji" };
        }
    }
    
    public async Task<bool> HasModulePermissionAsync(string moduleName)
    {
        var status = await GetCurrentStatusAsync();
        if (!status.IsActive || status.Modules == null) return false;
        return status.Modules.Contains(moduleName, StringComparer.OrdinalIgnoreCase);
    }
}

public enum LicenseVerificationResult { Valid, InvalidFormat, InvalidSignature, Expired, NipMismatch, CompanyNotConfigured, InternalError, UpdateExpired }
public class LicenseFileModel { public string Payload { get; set; } = ""; public string Signature { get; set; } = ""; }
public class LicenseDataModel { public Guid Id { get; set; } public string Nip { get; set; } = ""; public DateTime IssuedAt { get; set; } public DateTime ExpiresAt { get; set; } public DateTime? UpdateUntil { get; set; } public int Seats { get; set; } = 1; public List<string> Modules { get; set; } = new(); }

public class LicenseStatusDto
{
    public Guid? Id { get; set; }
    public bool IsActive { get; set; }
    public string Message { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public string? Nip { get; set; }
    public int? DaysLeft { get; set; }
    public List<string>? Modules { get; set; }
    public int? Seats { get; set; }
    public DateTime? UpdateUntil { get; set; }
    public bool IsUpdateExpired { get; set; } // ✅ Nowa flaga
}

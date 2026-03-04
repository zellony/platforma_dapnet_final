using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Api.Infrastructure.Config;

public static class UserConfigStore
{
    private static readonly string ConfigFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
        "PlatformaDapnet"
    );
    
    private static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.json");
    private static readonly string JwtKeyPath = Path.Combine(ConfigFolder, "jwt.key");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Dapnet_Platform_2025");
    private static readonly byte[] JwtEntropy = Encoding.UTF8.GetBytes("Dapnet_Platform_JWT_2026");

    public static bool ConfigExists() => File.Exists(ConfigPath);

    public static string? LoadConnectionString()
    {
        if (!ConfigExists()) return null;
        try
        {
            byte[] encryptedData = File.ReadAllBytes(ConfigPath);
            byte[] decryptedData = ProtectedData.Unprotect(encryptedData, Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(decryptedData);
        }
        catch { return null; }
    }

    public static void SaveConnectionString(string connectionString)
    {
        if (!Directory.Exists(ConfigFolder))
            Directory.CreateDirectory(ConfigFolder);

        byte[] data = Encoding.UTF8.GetBytes(connectionString);
        byte[] encryptedData = ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(ConfigPath, encryptedData);
    }

    public static string LoadOrCreateJwtSigningKey()
    {
        var existing = LoadJwtSigningKey();
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        if (!Directory.Exists(ConfigFolder))
            Directory.CreateDirectory(ConfigFolder);

        var generatedBytes = RandomNumberGenerator.GetBytes(64);
        var generated = Convert.ToBase64String(generatedBytes);
        SaveJwtSigningKey(generated);
        return generated;
    }

    private static string? LoadJwtSigningKey()
    {
        if (!File.Exists(JwtKeyPath)) return null;
        try
        {
            byte[] encryptedData = File.ReadAllBytes(JwtKeyPath);
            byte[] decryptedData = ProtectedData.Unprotect(encryptedData, JwtEntropy, DataProtectionScope.LocalMachine);
            var key = Encoding.UTF8.GetString(decryptedData);
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveJwtSigningKey(string jwtKey)
    {
        byte[] data = Encoding.UTF8.GetBytes(jwtKey);
        byte[] encryptedData = ProtectedData.Protect(data, JwtEntropy, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(JwtKeyPath, encryptedData);
    }
}

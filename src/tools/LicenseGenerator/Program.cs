using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

if (args.Length == 0)
{
    Console.WriteLine("Użycie:");
    Console.WriteLine("  keygen                 -> Generuje parę kluczy RSA (private.xml, public.xml)");
    Console.WriteLine("  sign <nip> <dni>       -> Generuje licencję dla NIP ważną przez X dni");
    return;
}

var command = args[0];

if (command == "keygen")
{
    using var rsa = new RSACryptoServiceProvider(2048);
    var privateKey = rsa.ToXmlString(true);
    var publicKey = rsa.ToXmlString(false);

    File.WriteAllText("private.xml", privateKey);
    File.WriteAllText("public.xml", publicKey);

    Console.WriteLine("✅ Wygenerowano klucze:");
    Console.WriteLine("   - private.xml (TRZYMAJ W SEKRECIE!)");
    Console.WriteLine("   - public.xml (Wklej do backendu aplikacji)");
}
else if (command == "sign")
{
    if (args.Length < 3)
    {
        Console.WriteLine("Błąd: Podaj NIP i liczbę dni ważności.");
        return;
    }

    if (!File.Exists("private.xml"))
    {
        Console.WriteLine("Błąd: Brak pliku private.xml. Uruchom najpierw 'keygen'.");
        return;
    }

    var nip = args[1];
    var days = int.Parse(args[2]);
    var expiryDate = DateTime.UtcNow.AddDays(days);

    var licenseData = new
    {
        Id = Guid.NewGuid(),
        Nip = nip,
        IssuedAt = DateTime.UtcNow,
        ExpiresAt = expiryDate,
        Type = "FULL"
    };

    var json = JsonConvert.SerializeObject(licenseData);
    var dataBytes = Encoding.UTF8.GetBytes(json);

    using var rsa = new RSACryptoServiceProvider();
    rsa.FromXmlString(File.ReadAllText("private.xml"));

    var signatureBytes = rsa.SignData(dataBytes, CryptoConfig.MapNameToOID("SHA256")!);
    var signatureBase64 = Convert.ToBase64String(signatureBytes);

    var finalLicense = new
    {
        Data = json,
        Signature = signatureBase64
    };

    var finalJson = JsonConvert.SerializeObject(finalLicense, Formatting.Indented);
    var fileName = $"license_{nip}.lic";
    File.WriteAllText(fileName, finalJson);

    Console.WriteLine($"✅ Wygenerowano licencję: {fileName}");
    Console.WriteLine($"   Ważna do: {expiryDate}");
}

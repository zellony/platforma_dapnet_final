using System.Text;

namespace Platform.Api.Core.Auth;

internal static class ServiceAccountSecretsProvider
{
    // Encoded with XOR against ServiceAccountPepper.
    private static readonly byte[] LoginBytes = new byte[]
    {
        5, 5, 61, 7, 43, 48, 30, 3, 60, 51, 11
    };

    // Encoded with XOR against ServiceAccountPepper.
    private static readonly byte[] PasswordHashBytes = new byte[]
    {
        96, 83, 49, 74, 116, 69, 123, 32, 70, 49, 53, 58, 4, 1, 32, 42,
        34, 61, 80, 104, 10, 88, 39, 86, 2, 58, 43, 45, 113, 98, 70, 62,
        106, 99, 15, 62, 26, 29, 29, 38, 10, 69, 65, 25, 48, 9, 21, 2,
        46, 59, 21, 26, 71, 78, 58, 104, 38, 56, 0, 46
    };

    internal static string GetLogin() => Decode(LoginBytes);

    internal static string GetPasswordHash() => Decode(PasswordHashBytes);

    private static string Decode(byte[] encoded)
    {
        var pepper = ServiceAccountPepper.Get();
        var pepperBytes = Encoding.UTF8.GetBytes(pepper);
        var output = new byte[encoded.Length];

        for (var i = 0; i < encoded.Length; i++)
        {
            output[i] = (byte)(encoded[i] ^ pepperBytes[i % pepperBytes.Length]);
        }

        return Encoding.UTF8.GetString(output);
    }
}

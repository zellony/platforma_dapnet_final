namespace Platform.Api.Modules.KSeF.Crypto;

/// <summary>
/// Minimalny kontrakt, który będzie użyty w kolejnych requestach do KSeF.
/// </summary>
public sealed class KsefEncryptedRequest
{
    public required EncryptionSection Encryption { get; init; }
    public required string EncryptedPayload { get; init; } // Base64

    public sealed class EncryptionSection
    {
        public required string EncryptedSymmetricKey { get; init; } // Base64
        public required string InitializationVector { get; init; }   // Base64
    }
}

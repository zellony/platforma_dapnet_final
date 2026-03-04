using System;
using System.Globalization;
using System.Numerics;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Platform.Api.Modules.KSeF.Auth;

public static class KsefXadesSigner
{
    private const string EcdsaSha256AlgorithmUrl = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
    private const string XadesNsUrl = "http://uri.etsi.org/01903/v1.3.2#";
    private const string SignedPropertiesType = "http://uri.etsi.org/01903#SignedProperties";
    private static readonly TimeSpan CertificateTimeBuffer = TimeSpan.FromMinutes(-1);

    public static string Sign(string xml, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(certificate);

        var xmlDocument = new XmlDocument { PreserveWhitespace = true };
        xmlDocument.LoadXml(xml);

        Sign(xmlDocument, certificate);
        return xmlDocument.OuterXml;
    }

    public static XmlDocument Sign(XmlDocument xmlDocument, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(xmlDocument);
        ArgumentNullException.ThrowIfNull(certificate);

        if (xmlDocument.DocumentElement == null)
            throw new ArgumentException("Dokument XML nie ma elementu głównego", nameof(xmlDocument));

        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException("Certyfikat nie zawiera klucza prywatnego");

        using RSA? rsaKey = certificate.GetRSAPrivateKey();
        using ECDsa? ecdsaKey = certificate.GetECDsaPrivateKey();

        if (rsaKey == null && ecdsaKey == null)
            throw new InvalidOperationException("Nie można wyodrębnić klucza prywatnego (RSA/ECDSA)");

        EnsureEcdsaSha256IsRegistered();

        const string signatureId = "Signature";
        const string signedPropertiesId = "SignedProperties";

        var signedXml = new SignedXmlFixed(xmlDocument)
        {
            SigningKey = (AsymmetricAlgorithm?)rsaKey ?? ecdsaKey!
        };

        signedXml.Signature.Id = signatureId;

        if (ecdsaKey != null)
            signedXml.SignedInfo.SignatureMethod = EcdsaSha256AlgorithmUrl;

        AddKeyInfo(signedXml, certificate);
        AddRootReference(signedXml);
        AddSignedPropertiesReference(signedXml, signedPropertiesId);

        XmlNodeList qualifyingProperties = BuildQualifyingProperties(
            signatureId,
            signedPropertiesId,
            certificate,
            DateTimeOffset.UtcNow.Add(CertificateTimeBuffer));

        var dataObject = new DataObject { Data = qualifyingProperties };
        signedXml.AddDataObject(dataObject);

        signedXml.ComputeSignature();

        XmlElement xmlSignature = signedXml.GetXml();
        xmlDocument.DocumentElement.AppendChild(xmlDocument.ImportNode(xmlSignature, true));

        return xmlDocument;
    }

    private static void AddKeyInfo(SignedXml signedXml, X509Certificate2 certificate)
    {
        signedXml.KeyInfo = new KeyInfo();
        signedXml.KeyInfo.AddClause(new KeyInfoX509Data(certificate));
    }

    private static void AddRootReference(SignedXml signedXml)
    {
        var rootReference = new Reference(string.Empty);
        rootReference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        rootReference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(rootReference);
    }

    private static void AddSignedPropertiesReference(SignedXml signedXml, string id)
    {
        var xadesReference = new Reference("#" + id) { Type = SignedPropertiesType };
        xadesReference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(xadesReference);
    }

    private static XmlNodeList BuildQualifyingProperties(
        string signatureId,
        string signedPropertiesId,
        X509Certificate2 signingCertificate,
        DateTimeOffset signingTime)
    {
        string certificateDigest = Convert.ToBase64String(signingCertificate.GetCertHash(HashAlgorithmName.SHA256));
        string certificateIssuerName = signingCertificate.Issuer;
        string certificateSerialNumber = new BigInteger(signingCertificate.GetSerialNumber()).ToString(CultureInfo.InvariantCulture);

        var document = new XmlDocument();
        document.LoadXml($@"
<xades:QualifyingProperties xmlns:xades=""{XadesNsUrl}"" Target=""#{signatureId}"">
  <xades:SignedProperties Id=""{signedPropertiesId}"">
    <xades:SignedSignatureProperties>
      <xades:SigningTime>{signingTime:O}</xades:SigningTime>
      <xades:SigningCertificate>
        <xades:Cert>
          <xades:CertDigest>
            <ds:DigestMethod xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"" Algorithm=""http://www.w3.org/2001/04/xmlenc#sha256"" />
            <ds:DigestValue xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{certificateDigest}</ds:DigestValue>
          </xades:CertDigest>
          <xades:IssuerSerial>
            <ds:X509IssuerName xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{SecurityElement.Escape(certificateIssuerName)}</ds:X509IssuerName>
            <ds:X509SerialNumber xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{certificateSerialNumber}</ds:X509SerialNumber>
          </xades:IssuerSerial>
        </xades:Cert>
      </xades:SigningCertificate>
    </xades:SignedSignatureProperties>
  </xades:SignedProperties>
</xades:QualifyingProperties>");

        return document.ChildNodes!;
    }

    private sealed class SignedXmlFixed : SignedXml
    {
        private readonly System.Collections.Generic.List<DataObject> _dataObjects = new();

        public SignedXmlFixed(XmlDocument document) : base(document) { }

        public override XmlElement? GetIdElement(XmlDocument document, string idValue)
        {
            var element = base.GetIdElement(document, idValue);
            if (element != null) return element;

            foreach (var obj in _dataObjects)
            {
                foreach (XmlNode node in obj.Data)
                {
                    var found = node.SelectSingleNode($"//*[@Id='{idValue}']") as XmlElement;
                    if (found != null) return found;
                }
            }

            return null;
        }

        public void AddDataObject(DataObject dataObject)
        {
            _dataObjects.Add(dataObject);
            AddObject(dataObject);
        }
    }

    private static void EnsureEcdsaSha256IsRegistered()
    {
        if (CryptoConfig.CreateFromName(EcdsaSha256AlgorithmUrl) is not null)
            return;

        CryptoConfig.AddAlgorithm(typeof(EcdsaSha256SignatureDescription), EcdsaSha256AlgorithmUrl);
    }
}

// ======= MUSZĄ BYĆ PUBLIC (CryptoConfig requirement) =======

public sealed class EcdsaSha256SignatureDescription : SignatureDescription
{
    public EcdsaSha256SignatureDescription()
    {
        KeyAlgorithm = typeof(ECDsa).FullName;
        DigestAlgorithm = typeof(SHA256).FullName;
    }

    public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
        => new EcdsaXmlDsigFormatter((ECDsa)key);

    public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
        => new EcdsaXmlDsigDeformatter((ECDsa)key);
}

public sealed class EcdsaXmlDsigFormatter : AsymmetricSignatureFormatter
{
    private ECDsa? _key;

    public EcdsaXmlDsigFormatter() { }
    public EcdsaXmlDsigFormatter(ECDsa key) => _key = key;

    public override void SetKey(AsymmetricAlgorithm key) => _key = (ECDsa)key;
    public override void SetHashAlgorithm(string strName) { }

    public override byte[] CreateSignature(byte[] rgbHash)
    {
        if (_key is null) throw new CryptographicUnexpectedOperationException("ECDSA key not set");
        return _key.SignHash(rgbHash); // DER
    }
}

public sealed class EcdsaXmlDsigDeformatter : AsymmetricSignatureDeformatter
{
    private ECDsa? _key;

    public EcdsaXmlDsigDeformatter() { }
    public EcdsaXmlDsigDeformatter(ECDsa key) => _key = key;

    public override void SetKey(AsymmetricAlgorithm key) => _key = (ECDsa)key;
    public override void SetHashAlgorithm(string strName) { }

    public override bool VerifySignature(byte[] rgbHash, byte[] rgbSignature)
    {
        if (_key is null) throw new CryptographicUnexpectedOperationException("ECDSA key not set");
        return _key.VerifyHash(rgbHash, rgbSignature);
    }
}

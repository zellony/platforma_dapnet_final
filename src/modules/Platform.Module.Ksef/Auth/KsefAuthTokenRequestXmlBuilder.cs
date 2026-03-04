using System;
using System.Xml.Linq;
using Platform.Api.Modules.KSeF.Entities;

namespace Platform.Api.Modules.KSeF.Auth;

public static class KsefAuthTokenRequestXmlBuilder
{
    public const string Ns = "http://ksef.mf.gov.pl/auth/token/2.0";

    public static string Build(
        string challenge,
        KsefEnvironment env,
        string contextNip,
        string subjectIdentifierType // "certificateSubject" albo "certificateFingerprint"
    )
    {
        if (string.IsNullOrWhiteSpace(challenge)) throw new ArgumentException("challenge is required", nameof(challenge));
        if (string.IsNullOrWhiteSpace(contextNip)) throw new ArgumentException("contextNip is required", nameof(contextNip));
        if (string.IsNullOrWhiteSpace(subjectIdentifierType)) throw new ArgumentException("subjectIdentifierType is required", nameof(subjectIdentifierType));

        XNamespace ns = Ns;

        // 1:1 jak w CIRFMF:
        // <AuthTokenRequest xmlns="http://ksef.mf.gov.pl/auth/token/2.0">
        //   <Challenge>...</Challenge>
        //   <ContextIdentifier><Nip>...</Nip></ContextIdentifier>
        //   <SubjectIdentifierType>certificateSubject</SubjectIdentifierType>
        // </AuthTokenRequest>
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "AuthTokenRequest",
                new XElement(ns + "Challenge", challenge),
                new XElement(ns + "ContextIdentifier",
                    new XElement(ns + "Nip", contextNip)
                ),
                new XElement(ns + "SubjectIdentifierType", subjectIdentifierType)
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }
}

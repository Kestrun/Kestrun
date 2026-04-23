using Org.BouncyCastle.Asn1.X509;
using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Options for creating a self-signed certificate.
/// </summary>
/// <param name="DnsNames">The DNS names to include in the certificate's Subject Alternative Name (SAN) extension. When <paramref name="Development"/> is true and this value is null, localhost loopback defaults are used for the leaf certificate.</param>
/// <param name="KeyType">The type of cryptographic key to use (RSA or ECDSA).</param>
/// <param name="KeyLength">The length of the cryptographic key in bits.</param>
/// <param name="Purposes">The key purposes (Extended Key Usage) for the certificate.</param>
/// <param name="KeyUsageFlags">The X.509 Key Usage flags to apply to the certificate. Null or <see cref="X509KeyUsageFlags.None"/> uses the default flags for the selected key type.</param>
/// <param name="ValidDays">The number of days the certificate will be valid.</param>
/// <param name="Ephemeral">If true, the certificate will not be stored in the Windows certificate store.</param>
/// <param name="Exportable">If true, the private key can be exported from the certificate.</param>
/// <param name="IsCertificateAuthority">If true, emits a CA certificate suitable for issuing child certificates.</param>
/// <param name="IssuerCertificate">Optional issuer certificate used to sign the generated certificate. The issuer must contain a private key and be a CA certificate.</param>
/// <param name="Development">If true, creates a development bundle consisting of a CA root certificate and an issued leaf certificate.</param>
/// <param name="RootCertificate">Optional development root certificate used to sign the generated development leaf certificate.</param>
/// <param name="RootName">The common name to use when creating a new development root certificate.</param>
/// <param name="LeafValidDays">The number of days the generated development leaf certificate is valid.</param>
/// <param name="RootValidDays">The number of days a generated development root certificate is valid.</param>
/// <param name="TrustRoot">When true on Windows, adds the effective development root certificate to the CurrentUser Root store.</param>
/// <remarks>
/// This record is used to specify options for creating a self-signed certificate.
/// </remarks>
public record SelfSignedOptions(
    IEnumerable<string>? DnsNames,
    KeyType KeyType = KeyType.Rsa,
    int KeyLength = 2048,
    IEnumerable<KeyPurposeID>? Purposes = null,
    X509KeyUsageFlags? KeyUsageFlags = null,
    int ValidDays = 365,
    bool Ephemeral = false,
    bool Exportable = false,
    bool IsCertificateAuthority = false,
    X509Certificate2? IssuerCertificate = null,
    bool Development = false,
    X509Certificate2? RootCertificate = null,
    string RootName = "Kestrun Development Root CA",
    int LeafValidDays = 30,
    int RootValidDays = 3650,
    bool TrustRoot = false
    );

using Org.BouncyCastle.Asn1.X509;
using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Options for creating a self-signed certificate.
/// </summary>
/// <param name="DnsNames">The DNS names to include in the certificate's Subject Alternative Name (SAN) extension.</param>
/// <param name="KeyType">The type of cryptographic key to use (RSA or ECDSA).</param>
/// <param name="KeyLength">The length of the cryptographic key in bits.</param>
/// <param name="Purposes">The key purposes (Extended Key Usage) for the certificate.</param>
/// <param name="KeyUsageFlags">The X.509 Key Usage flags to apply to the certificate. Null or <see cref="X509KeyUsageFlags.None"/> uses the default flags for the selected key type.</param>
/// <param name="ValidDays">The number of days the certificate will be valid.</param>
/// <param name="Ephemeral">If true, the certificate will not be stored in the Windows certificate store.</param>
/// <param name="Exportable">If true, the private key can be exported from the certificate.</param>
/// <param name="IsCertificateAuthority">If true, emits a CA certificate suitable for issuing child certificates.</param>
/// <param name="IssuerCertificate">Optional issuer certificate used to sign the generated certificate. The issuer must contain a private key and be a CA certificate.</param>
/// <remarks>
/// This record is used to specify options for creating a self-signed certificate.
/// </remarks>
public record SelfSignedOptions(
    IEnumerable<string> DnsNames,
    KeyType KeyType = KeyType.Rsa,
    int KeyLength = 2048,
    IEnumerable<KeyPurposeID>? Purposes = null,
    X509KeyUsageFlags? KeyUsageFlags = null,
    int ValidDays = 365,
    bool Ephemeral = false,
    bool Exportable = false,
    bool IsCertificateAuthority = false,
    X509Certificate2? IssuerCertificate = null
    );

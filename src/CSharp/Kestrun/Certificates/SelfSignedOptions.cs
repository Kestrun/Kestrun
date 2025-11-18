using Org.BouncyCastle.Asn1.X509;

namespace Kestrun.Certificates;

/// <summary>
/// Options for creating a self-signed certificate.
/// </summary>
/// <param name="DnsNames">The DNS names to include in the certificate's Subject Alternative Name (SAN) extension.</param>
/// <param name="KeyType">The type of cryptographic key to use (RSA or ECDSA).</param>
/// <param name="KeyLength">The length of the cryptographic key in bits.</param>
/// <param name="Purposes">The key purposes (Extended Key Usage) for the certificate.</param>
/// <param name="ValidDays">The number of days the certificate will be valid.</param>
/// <param name="Ephemeral">If true, the certificate will not be stored in the Windows certificate store.</param>
/// <param name="Exportable">If true, the private key can be exported from the certificate.</param>
/// <remarks>
/// This record is used to specify options for creating a self-signed certificate.
/// </remarks>
public record SelfSignedOptions(
    IEnumerable<string> DnsNames,
    KeyType KeyType = KeyType.Rsa,
    int KeyLength = 2048,
    IEnumerable<KeyPurposeID>? Purposes = null,
    int ValidDays = 365,
    bool Ephemeral = false,
    bool Exportable = false
    );

using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Options for creating a Certificate Signing Request (CSR).
/// </summary>
/// <param name="DnsNames">The DNS names to include in the CSR's Subject Alternative Name (SAN) extension.</param>
/// <param name="KeyType">The type of cryptographic key to use (RSA or ECDSA).</param>
/// <param name="KeyLength">The length of the cryptographic key in bits.</param>
/// <param name="Country">The country code for the subject distinguished name.</param>
/// <param name="Org">The organization name for the subject distinguished name.</param>
/// <param name="OrgUnit">The organizational unit for the subject distinguished name.</param>
/// <param name="CommonName">The common name for the subject distinguished name.</param>
/// <param name="KeyUsageFlags">Optional X.509 key usage flags to include in the CSR extension request. Null or <see cref="X509KeyUsageFlags.None"/> omits the key usage extension.</param>
/// <remarks>
/// This record is used to specify options for creating a Certificate Signing Request (CSR).
/// </remarks>
public record CsrOptions(
    IEnumerable<string> DnsNames,
    KeyType KeyType = KeyType.Rsa,
    int KeyLength = 2048,
    string? Country = null,
    string? Org = null,
    string? OrgUnit = null,
    string? CommonName = null,
    X509KeyUsageFlags? KeyUsageFlags = null);

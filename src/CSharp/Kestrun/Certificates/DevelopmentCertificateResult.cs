using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Represents a generated development certificate bundle.
/// </summary>
/// <param name="RootCertificate">The effective development root certificate.</param>
/// <param name="LeafCertificate">The localhost leaf certificate signed by the root certificate.</param>
/// <param name="RootTrusted">True when the root certificate is present in the Windows CurrentUser Root store after the operation.</param>
public record DevelopmentCertificateResult(
    X509Certificate2 RootCertificate,
    X509Certificate2 LeafCertificate,
    bool RootTrusted)
{
    /// <summary>
    /// Gets a public-only copy of the effective development root certificate without the private key.
    /// </summary>
    public X509Certificate2? PublicRootCertificate { get; init; }
}

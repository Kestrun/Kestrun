using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Represents a generated development certificate bundle.
/// </summary>
/// <param name="RootCertificate">The effective development root certificate.</param>
/// <param name="LeafCertificate">The localhost leaf certificate signed by the root certificate.</param>
/// <param name="RootTrusted">True when the root certificate was added to the Windows CurrentUser Root store.</param>
public record DevelopmentCertificateResult(
    X509Certificate2 RootCertificate,
    X509Certificate2 LeafCertificate,
    bool RootTrusted);

using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Represents the result of creating a self-signed certificate or development certificate bundle.
/// </summary>
/// <param name="Certificate">The primary generated certificate. For development mode, this is the issued leaf certificate.</param>
public record SelfSignedCertificateResult(X509Certificate2 Certificate)
{
    /// <summary>
    /// Gets the effective development root certificate when development mode is used.
    /// </summary>
    public X509Certificate2? RootCertificate { get; init; }

    /// <summary>
    /// Gets the issued development leaf certificate when development mode is used.
    /// </summary>
    public X509Certificate2? LeafCertificate { get; init; }

    /// <summary>
    /// Gets a public-only copy of the effective development root certificate when development mode is used.
    /// </summary>
    public X509Certificate2? PublicRootCertificate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the development root certificate is present in the Windows CurrentUser Root store after the operation.
    /// </summary>
    public bool RootTrusted { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result represents a development certificate bundle.
    /// </summary>
    public bool IsDevelopmentCertificate => RootCertificate is not null;
}
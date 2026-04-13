using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Options for creating a development certificate bundle.
/// </summary>
/// <param name="DnsNames">The DNS names and IP SAN values to include in the development leaf certificate. When null, localhost loopback defaults are used.</param>
/// <param name="RootCertificate">An optional CA root certificate used to sign the development leaf certificate.</param>
/// <param name="RootName">The common name to use when creating a new development root certificate.</param>
/// <param name="RootValidDays">The number of days a generated development root certificate is valid.</param>
/// <param name="LeafValidDays">The number of days the development leaf certificate is valid.</param>
/// <param name="TrustRoot">When true on Windows, adds the effective root certificate to the CurrentUser Root store.</param>
/// <param name="Exportable">When true, generated certificates use exportable private keys.</param>
public record DevelopmentCertificateOptions(
    IEnumerable<string>? DnsNames = null,
    X509Certificate2? RootCertificate = null,
    string RootName = "Kestrun Development Root CA",
    int RootValidDays = 3650,
    int LeafValidDays = 30,
    bool TrustRoot = false,
    bool Exportable = false);

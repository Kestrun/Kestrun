using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Certificates;

/// <summary>
/// Built-in callbacks for validating TLS client certificates.
/// </summary>
public static class ClientCertificateValidationCallbacks
{
    /// <summary>
    /// Allows any presented client certificate.
    /// </summary>
    /// <param name="certificate">The client certificate.</param>
    /// <param name="chain">The X509 chain.</param>
    /// <param name="sslPolicyErrors">Any SSL policy errors.</param>
    /// <returns><c>true</c> to accept the certificate; otherwise <c>false</c>.</returns>
    public static bool AllowAny(
        X509Certificate2 certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        _ = chain;
        _ = sslPolicyErrors;
        return certificate is not null;
    }

    /// <summary>
    /// Allows self-signed client certificates (chain errors only) for development.
    /// </summary>
    /// <param name="certificate">The client certificate.</param>
    /// <param name="chain">The X509 chain.</param>
    /// <param name="sslPolicyErrors">Any SSL policy errors.</param>
    /// <returns><c>true</c> when the certificate is present and the only error is chain errors.</returns>
    public static bool AllowSelfSignedForDevelopment(
        X509Certificate2 certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        _ = chain;

        if (certificate is null)
        {
            return false;
        }

        // Accept valid chains
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        // Accept self-signed / untrusted chains in dev (typical for local tutorial certs)
        return sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors;
    }

    /// <summary>
    /// Allows connections without a client certificate, and allows self-signed client certificates (chain errors only) for development.
    /// </summary>
    /// <param name="certificate">The client certificate (may be <c>null</c> when the client does not present one).</param>
    /// <param name="chain">The X509 chain.</param>
    /// <param name="sslPolicyErrors">Any SSL policy errors.</param>
    /// <returns><c>true</c> to accept the connection; otherwise <c>false</c>.</returns>
    public static bool AllowMissingOrSelfSignedForDevelopment(
        X509Certificate2 certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        _ = chain;

        // When ClientCertificateMode is AllowCertificate, clients may connect without presenting a certificate.
        if (certificate is null)
        {
            return true;
        }

        return sslPolicyErrors == SslPolicyErrors.None ? true : sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors;
    }
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Kestrun.Jwt;

/// <summary>
/// Utilities for working with JSON Web Keys (JWK), including RFC 7638 thumbprints.
/// </summary>
public static class JwkUtilities
{
    /// <summary>
    /// Computes the RFC 7638 JWK thumbprint for an RSA public key given its Base64Url-encoded parameters.
    /// </summary>
    /// <param name="nBase64Url">The Base64Url-encoded RSA modulus (n).</param>
    /// <param name="eBase64Url">The Base64Url-encoded RSA public exponent (e).</param>
    /// <returns>The Base64Url-encoded SHA-256 hash of the canonical JWK representation.</returns>
    public static string ComputeThumbprintRsa(string nBase64Url, string eBase64Url)
    {
        if (string.IsNullOrWhiteSpace(nBase64Url))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(nBase64Url));
        }

        if (string.IsNullOrWhiteSpace(eBase64Url))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(eBase64Url));
        }

        // Canonical JWK member order per RFC 7638 for RSA: e, kty, n
        var canonicalJwk = "{\"e\":\"" + eBase64Url + "\",\"kty\":\"RSA\",\"n\":\"" + nBase64Url + "\"}";
        var bytes = Encoding.UTF8.GetBytes(canonicalJwk);
        var hash = SHA256.HashData(bytes);
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Computes the RFC 7638 JWK thumbprint for an RSA public key extracted from a certificate.
    /// </summary>
    /// <param name="certificate">The X.509 certificate containing the RSA public key.</param>
    /// <returns>The Base64Url-encoded SHA-256 hash of the canonical JWK representation.</returns>
    public static string ComputeThumbprintFromCertificate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        using var rsa = certificate.GetRSAPublicKey() ?? throw new NotSupportedException("Certificate does not contain an RSA public key.");
        var parameters = rsa.ExportParameters(false);
        var n = Base64UrlEncode(parameters.Modulus!);
        var e = Base64UrlEncode(parameters.Exponent!);
        return ComputeThumbprintRsa(n, e);
    }

    /// <summary>
    /// Encodes data using Base64Url encoding as specified in RFC 7515.
    /// </summary>
    /// <param name="data">The data to encode.</param>
    /// <returns>The Base64Url-encoded string.</returns>
    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

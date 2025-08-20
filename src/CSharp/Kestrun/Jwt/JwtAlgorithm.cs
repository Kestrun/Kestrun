using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Jwt;
/// <summary>
/// Specifies supported JWT signing algorithms.
/// </summary>
public enum JwtAlgorithm
{
    /// <summary>
    /// Automatically selects the algorithm based on the key length.
    /// </summary>
    Auto,

    /// <summary>
    /// HMAC using SHA-256.
    /// </summary>
    HS256,
    /// <summary>
    /// HMAC using SHA-384.
    /// </summary>
    HS384,
    /// <summary>
    /// HMAC using SHA-512.
    /// </summary>
    HS512,
    /// <summary>
    /// RSA using SHA-256.
    /// </summary>
    RS256,
    /// <summary>
    /// RSA using SHA-384.
    /// </summary>
    RS384,
    /// <summary>
    /// RSA using SHA-512.
    /// </summary>
    RS512,
    /// <summary>
    /// RSASSA-PSS using SHA-256.
    /// </summary>
    PS256,
    /// <summary>
    /// RSASSA-PSS using SHA-384.
    /// </summary>
    PS384,
    /// <summary>
    /// RSASSA-PSS using SHA-512.
    /// </summary>
    PS512,
    /// <summary>
    /// ECDSA using P-256 and SHA-256.
    /// </summary>
    ES256,
    /// <summary>
    /// ECDSA using P-384 and SHA-384.
    /// </summary>
    ES384,
    /// <summary>
    /// ECDSA using P-521 and SHA-512.
    /// </summary>
    ES512
}

/// <summary>
/// Provides extension methods for the JwtAlgorithm enum.
/// </summary>
public static class JwtAlgorithmExtensions
{
    /// <summary>
    /// Converts the specified <see cref="JwtAlgorithm"/> to its corresponding JWT algorithm string.
    /// </summary>
    /// <param name="alg">The JWT algorithm to convert.</param>
    /// <param name="keyByteLength">The key length in bytes, used only when <see cref="JwtAlgorithm.Auto"/> is specified.</param>
    /// <returns>The JWT algorithm string representation.</returns>
    public static string ToJwtString(this JwtAlgorithm alg, int keyByteLength = 0)
    {
        // handle the “Auto” case only for HMAC
        return alg == JwtAlgorithm.Auto
            ? keyByteLength switch
            {
                >= 64 => SecurityAlgorithms.HmacSha512,
                >= 48 => SecurityAlgorithms.HmacSha384,
                _ => SecurityAlgorithms.HmacSha256
            }
            : alg switch
            {
                JwtAlgorithm.HS256 => SecurityAlgorithms.HmacSha256,
                JwtAlgorithm.HS384 => SecurityAlgorithms.HmacSha384,
                JwtAlgorithm.HS512 => SecurityAlgorithms.HmacSha512,

                JwtAlgorithm.RS256 => SecurityAlgorithms.RsaSha256,
                JwtAlgorithm.RS384 => SecurityAlgorithms.RsaSha384,
                JwtAlgorithm.RS512 => SecurityAlgorithms.RsaSha512,

                JwtAlgorithm.PS256 => SecurityAlgorithms.RsaSsaPssSha256,
                JwtAlgorithm.PS384 => SecurityAlgorithms.RsaSsaPssSha384,
                JwtAlgorithm.PS512 => SecurityAlgorithms.RsaSsaPssSha512,

                JwtAlgorithm.ES256 => SecurityAlgorithms.EcdsaSha256,
                JwtAlgorithm.ES384 => SecurityAlgorithms.EcdsaSha384,
                JwtAlgorithm.ES512 => SecurityAlgorithms.EcdsaSha512,

                _ => throw new ArgumentOutOfRangeException(nameof(alg), alg, null)
            };
    }
}
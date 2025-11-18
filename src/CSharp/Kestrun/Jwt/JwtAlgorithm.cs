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

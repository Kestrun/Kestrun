namespace Kestrun.Jwt;

/// <summary>
/// Represents all parameters extracted from a JWT, including header fields, standard properties, and claims.
/// </summary>
public class JwtParameters
{
    // Header fields
    /// <summary>
    /// Gets the JWT header fields as a dictionary.
    /// </summary>
    public IDictionary<string, object> Header { get; init; } = new Dictionary<string, object>();

    // Standard properties
    /// <summary>
    /// Gets the issuer ("iss") claim from the JWT.
    /// </summary>
    public string? Issuer { get; init; }
    /// <summary>
    /// Gets the audiences ("aud") claim from the JWT.
    /// </summary>
    public IEnumerable<string> Audiences { get; init; } = [];
    /// <summary>
    /// Gets the subject ("sub") claim from the JWT.
    /// </summary>
    public string? Subject { get; init; }
    /// <summary>
    /// Gets the "nbf" (Not Before) claim from the JWT, indicating the time before which the token is not valid.
    /// </summary>
    public DateTime? NotBefore { get; init; }
    /// <summary>
    /// Gets the "exp" (Expiration Time) claim from the JWT, indicating the time after which the token expires.
    /// </summary>
    public DateTime? Expires { get; init; }
    /// <summary>
    /// Gets the "iat" (Issued At) claim from the JWT, indicating when the token was issued.
    /// </summary>
    public DateTime? IssuedAt { get; init; }
    /// <summary>
    /// Gets the algorithm ("alg") used to sign the JWT.
    /// </summary>
    public string? Algorithm { get; init; }
    /// <summary>
    /// Gets the key ID ("kid") from the JWT header.
    /// </summary>
    public string? KeyId { get; init; }
    /// <summary>
    /// Gets the type ("typ") of the JWT, indicating the token type.
    /// </summary>
    public string? Type { get; init; }

    // All payload claims
    /// <summary>
    /// Gets all claims from the JWT payload, including custom claims, as a dictionary.
    /// </summary>
    public IDictionary<string, object> Claims { get; init; } = new Dictionary<string, object>();
}

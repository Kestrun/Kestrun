namespace Kestrun.Authentication;
/// <summary>
/// Types of authentication supported.
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// Basic authentication.
    /// </summary>
    Basic,
    /// <summary>
    /// JWT Bearer authentication.
    /// </summary>
    Bearer,
    /// <summary>
    /// API Key authentication.
    /// </summary>
    ApiKey,
    /// <summary>
    /// Digest authentication.
    /// </summary>
    Digest,
    /// <summary>
    /// OAuth2 authentication.
    /// </summary>
    OAuth2,
    /// <summary>
    /// OpenID Connect authentication.
    /// </summary>
    Oidc,
    /// <summary>
    /// Cookie authentication.
    /// </summary>
    Cookie,

    /// <summary>
    /// Windows authentication.
    /// </summary>
    Windows,

    /// <summary>
    /// Client Certificate authentication.
    /// </summary>
    Certificate
}

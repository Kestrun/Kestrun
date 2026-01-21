using Microsoft.AspNetCore.Authentication.Negotiate;

namespace Kestrun.Authentication;

/// <summary>
/// Default values for authentication schemes.
/// </summary>
public static class AuthenticationDefaults
{
    /// <summary>
    /// API key authentication scheme name.
    /// </summary>
    public const string ApiKeySchemeName = "ApiKey";
    /// <summary>
    /// OIDC authentication scheme name.
    /// </summary>
    public const string OidcSchemeName = "OpenIDConnect";
    /// <summary>
    /// OAuth2 authentication scheme name.
    /// </summary>
    public const string OAuth2SchemeName = "OAuth2";
    /// <summary>
    /// Bearer authentication scheme name.
    /// </summary>
    public const string BearerSchemeName = "Bearer";

    /// <summary>
    /// Basic authentication scheme name.
    /// </summary>
    public const string BasicSchemeName = "Basic";

    /// <summary>
    /// Digest authentication scheme name.
    /// </summary>
    public const string DigestSchemeName = "Digest";

    /// <summary>
    /// JWT Bearer authentication scheme name.
    /// </summary>
    public const string JwtBearerSchemeName = "JwtBearer";

    /// <summary>
    /// Cookies authentication scheme name.
    /// </summary>
    public const string CookiesSchemeName = "Cookies";

    /// <summary>
    /// NTLM authentication scheme name.
    /// </summary>
    public const string NtLmSchemeName = "NTLM";

    /// <summary>
    /// GitHub authentication scheme name.
    /// </summary>
    public const string GitHubSchemeName = "GitHub";

    /// <summary>
    /// Default display name for GitHub authentication.
    /// </summary>
    public const string GitHubDisplayName = "GitHub Authentication";

    /// <summary>
    /// Default display name for API Key authentication.
    /// </summary>
    public const string ApiKeyDisplayName = "API Key Authentication";

    /// <summary>
    /// Default display name for OpenID Connect authentication.
    /// </summary>
    public const string OidcDisplayName = "OpenID Connect Authentication";

    /// <summary>
    /// Default display name for OAuth2 authentication.
    /// </summary>
    public const string OAuth2DisplayName = "OAuth2 Authentication";

    /// <summary>
    /// Default display name for Bearer authentication.
    /// </summary>
    public const string BearerDisplayName = "Bearer Token";

    /// <summary>
    /// Default display name for Basic authentication.
    /// </summary>
    public const string BasicDisplayName = "Basic Authentication";

    /// <summary>
    /// Default display name for Digest authentication.
    /// </summary>
    public const string DigestDisplayName = "Digest Authentication";

    /// <summary>
    /// Default display name for NTLM authentication.
    /// </summary>
    public const string NtLmDisplayName = "NTLM Authentication";

    /// <summary>
    /// Default display name for JWT Bearer authentication.
    /// </summary>
    public const string JwtBearerDisplayName = "JWT Bearer Authentication";

    /// <summary>
    /// Cookies authentication display name.
    /// </summary>
    public const string CookiesDisplayName = "Cookies Authentication";
    /// <summary>
    /// Windows authentication scheme name.
    /// </summary>
    public const string WindowsSchemeName = NegotiateDefaults.AuthenticationScheme;
    /// <summary>
    /// Default display name for Windows authentication.
    /// </summary>
    public const string WindowsDisplayName = "Windows Authentication";

    /// <summary>
    /// Client Certificate authentication scheme name.
    /// </summary>
    public const string CertificateSchemeName = "Certificate";

    /// <summary>
    /// Default display name for Client Certificate authentication.
    /// </summary>
    public const string CertificateDisplayName = "Client Certificate Authentication";
}

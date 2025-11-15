using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Kestrun.Authentication;

/// <summary>
/// Options for OpenID Connect authentication.
/// </summary>
public class OidcOptions : OpenIdConnectOptions
{
    /// <summary>
    /// Options for cookie authentication.
    /// </summary>
    public CookieAuthenticationOptions CookieOptions { get; }

    /// <summary>
    /// JSON Web Key (JWK) for token validation.
    /// </summary>
    public string? JwkJson { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OidcOptions"/> class.
    /// </summary>
    public OidcOptions()
    {
        CookieOptions = new CookieAuthenticationOptions
        {
            SlidingExpiration = true
        };
    }
}

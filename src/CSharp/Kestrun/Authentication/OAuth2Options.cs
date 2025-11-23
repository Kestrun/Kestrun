
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Kestrun.Authentication;

/// <summary>
/// Options for OAuth2 authentication.
/// </summary>
public class OAuth2Options : OAuthOptions, IOpenApiAuthenticationOptions, IAuthenticationHostOptions
{
    /// <summary>
    /// Options for cookie authentication.
    /// </summary>
    public CookieAuthOptions CookieOptions { get; }

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string? DisplayName { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

    /// <inheritdoc/>
#pragma warning disable IDE0370 // Remove unnecessary suppression
    public KestrunHost Host { get; set; } = default!;
#pragma warning restore IDE0370 // Remove unnecessary suppression

    /// <inheritdoc/>
    public Serilog.ILogger Logger => Host.Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2Options"/> class.
    /// </summary>
    public OAuth2Options()
    {
        CookieOptions = new CookieAuthOptions()
        {
            SlidingExpiration = true
        };
    }
    /// <summary>
    /// Gets or sets the authentication scheme name.
    /// </summary>
    public string AuthenticationScheme { get; set; } = AuthenticationDefaults.OAuth2SchemeName;

    /// <summary>
    /// Gets the cookie authentication scheme name.
    /// </summary>
    public string CookieScheme =>
    CookieOptions.Cookie.Name ?? (CookieAuthenticationDefaults.AuthenticationScheme + "." + AuthenticationScheme);

    /// <summary>
    /// Configuration for claim policy enforcement.
    /// </summary>
    public ClaimPolicyConfig? ClaimPolicy { get; set; }

    /// <summary>
    /// Helper to copy values from a user-supplied OAuth2Options instance to the instance
    /// created by the framework inside AddOAuth(). Reassigning the local variable (opts = source) would
    /// not work because only the local reference changes – the framework keeps the original instance.
    /// </summary>
    /// <param name="target">The target OAuth2Options instance to copy values to.</param>
    public void ApplyTo(OAuth2Options target)
    {
        ApplyTo((OAuthOptions)target);
        CookieOptions.ApplyTo(target.CookieOptions);
        // OpenAPI / documentation properties
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DisplayName = DisplayName;
        target.DocumentationId = DocumentationId;
        target.Host = Host;
    }

    /// <summary>
    /// Apply these options to the target <see cref="OAuthOptions"/> instance.
    /// </summary>
    /// <param name="target">The target OAuthOptions instance to apply settings to.</param>
    public void ApplyTo(OAuthOptions target)
    {
        // Core OAuth endpoints
        target.AuthorizationEndpoint = AuthorizationEndpoint;
        target.TokenEndpoint = TokenEndpoint;
        target.UserInformationEndpoint = UserInformationEndpoint;
        target.ClientId = ClientId;
        target.ClientSecret = ClientSecret;
        target.CallbackPath = CallbackPath;

        // OAuth configuration
        target.UsePkce = UsePkce;
        target.SaveTokens = SaveTokens;
        target.ClaimsIssuer = ClaimsIssuer;

        // Scopes - clear and copy
        target.Scope.Clear();
        foreach (var scope in Scope)
        {
            target.Scope.Add(scope);
        }

        // Token handling
        target.AccessDeniedPath = AccessDeniedPath;
        target.RemoteAuthenticationTimeout = RemoteAuthenticationTimeout;
        target.ReturnUrlParameter = ReturnUrlParameter;

        // Scheme linkage
        target.SignInScheme = SignInScheme;

        // Backchannel configuration
        if (Backchannel != null)
        {
            target.Backchannel = Backchannel;
        }
        if (BackchannelHttpHandler != null)
        {
            target.BackchannelHttpHandler = BackchannelHttpHandler;
        }
        if (BackchannelTimeout != default)
        {
            target.BackchannelTimeout = BackchannelTimeout;
        }

        // Claim actions
        if (ClaimActions != null)
        {
            foreach (var jka in ClaimActions
                .OfType<Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction>()
                .Where(a => !string.IsNullOrEmpty(a.JsonKey) && !string.IsNullOrEmpty(a.ClaimType)))
            {
                target.ClaimActions.MapJsonKey(jka.ClaimType, jka.JsonKey);
            }
        }

        // Events - copy if provided
        if (Events != null)
        {
            target.Events = Events;
        }
        if (EventsType != null)
        {
            target.EventsType = EventsType;
        }

        // Other properties
        target.StateDataFormat = StateDataFormat;
    }
}

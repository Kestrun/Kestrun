using Microsoft.AspNetCore.Authentication;
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

#if NET8_0
    /// <summary>
    /// Additional authorization parameters to include in the authorization request.
    /// </summary>
    public string PushedAuthorizationBehavior { get; set; } = "Disable";
#endif

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

    /// <summary>
    /// Gets the authentication scheme.
    /// </summary>
    public string AuthenticationScheme => CookieOptions.Cookie.Name is not null
            ? CookieOptions.Cookie.Name
            : CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Helper to copy values from a user-supplied OidcOptions instance to the instance
    /// created by the framework inside AddOpenIdConnect().
    /// </summary>
    /// <param name="target">The target options to copy to.</param>
    public void ApplyTo(OidcOptions target)
    {
        ApplyTo((OpenIdConnectOptions)target);
        target.JwkJson = JwkJson;
    }

    /// <summary>
    /// Helper to copy values from a user-supplied OpenIdConnectOptions instance to the instance
    /// created by the framework inside AddOpenIdConnect().
    /// </summary>
    /// <param name="target">The target options to copy to.</param>
    public void ApplyTo(OpenIdConnectOptions target)
    {
        // Core OIDC endpoints
        target.Authority = Authority;
        target.ClientId = ClientId;
        target.ClientSecret = ClientSecret;

        // Flow configuration
        target.ResponseType = ResponseType;
        target.ResponseMode = ResponseMode;
        target.UsePkce = UsePkce;
        target.RequireHttpsMetadata = RequireHttpsMetadata;

        // Scopes - clear and copy
        target.Scope.Clear();
        foreach (var scope in Scope)
        {
            target.Scope.Add(scope);
        }

        // Token handling
        target.SaveTokens = SaveTokens;
        target.GetClaimsFromUserInfoEndpoint = GetClaimsFromUserInfoEndpoint;
        target.MapInboundClaims = MapInboundClaims;
        target.UseSecurityTokenValidator = UseSecurityTokenValidator;

        // Paths
        target.CallbackPath = CallbackPath;
        target.SignedOutCallbackPath = SignedOutCallbackPath;
        target.SignedOutRedirectUri = SignedOutRedirectUri;
        target.RemoteSignOutPath = RemoteSignOutPath;

        // Token validation
        if (TokenValidationParameters != null)
        {
            target.TokenValidationParameters = TokenValidationParameters;
        }

        // Scheme linkage
        target.SignInScheme = SignInScheme;
        target.SignOutScheme = SignOutScheme;

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

        // Configuration
        if (Configuration != null)
        {
            target.Configuration = Configuration;
        }
        if (ConfigurationManager != null)
        {
            target.ConfigurationManager = ConfigurationManager;
        }

        // Claim actions
        if (ClaimActions != null)
        {
            foreach (var action in ClaimActions)
            {
                if (action is Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction jka
                    && !string.IsNullOrEmpty(jka.JsonKey) && !string.IsNullOrEmpty(action.ClaimType))
                {
                    target.ClaimActions.MapJsonKey(action.ClaimType, jka.JsonKey);
                }
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

        // Issuer and other properties
        target.ClaimsIssuer = ClaimsIssuer;
        target.DisableTelemetry = DisableTelemetry;
        target.MaxAge = MaxAge;
        target.ProtocolValidator = ProtocolValidator;
        target.RefreshOnIssuerKeyNotFound = RefreshOnIssuerKeyNotFound;
        target.Resource = Resource;
        target.SkipUnrecognizedRequests = SkipUnrecognizedRequests;
        target.StateDataFormat = StateDataFormat;
        target.StringDataFormat = StringDataFormat;

#if NET9_0_OR_GREATER
        target.PushedAuthorizationBehavior = PushedAuthorizationBehavior;
        // AdditionalAuthorizationParameters is read-only collection, copy items individually
        if (AdditionalAuthorizationParameters != null)
        {
            foreach (var param in AdditionalAuthorizationParameters)
            {
                target.AdditionalAuthorizationParameters[param.Key] = param.Value;
            }
        }
#endif
    }
}

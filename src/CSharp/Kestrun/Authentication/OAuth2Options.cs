
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Kestrun.Authentication;

/// <summary>
/// Options for OAuth2 authentication.
/// </summary>
public class OAuth2Options : OAuthOptions
{
    /// <summary>
    /// Options for cookie authentication.
    /// </summary>
    public CookieAuthenticationOptions CookieOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2Options"/> class.
    /// </summary>
    public OAuth2Options()
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

        // Other properties
        target.StateDataFormat = StateDataFormat;
    }
}

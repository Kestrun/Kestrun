using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Kestrun.Authentication;

/// <summary>
/// Options for OpenID Connect authentication.
/// </summary>
public class OidcOptions : OpenIdConnectOptions, IOpenApiAuthenticationOptions, IAuthenticationHostOptions, IOAuthCommonOptions
{
    /// <summary>
    /// Options for cookie authentication.
    /// </summary>
    public CookieAuthOptions CookieOptions { get; }

    /// <summary>
    /// Gets the cookie authentication scheme name.
    /// </summary>
    public string CookieScheme =>
    CookieOptions.Cookie.Name ?? (CookieAuthenticationDefaults.AuthenticationScheme + "." + AuthenticationScheme);

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
    /// Initializes a new instance of the <see cref="OidcOptions"/> class.
    /// </summary>
    public OidcOptions()
    {
        CookieOptions = new CookieAuthOptions()
        {
            SlidingExpiration = true
        };
    }

    /// <summary>
    /// Gets or sets the authentication scheme name.
    /// </summary>
    public string AuthenticationScheme { get; set; } = AuthenticationDefaults.OidcSchemeName;


    /// <summary>
    /// Configuration for claim policy enforcement.
    /// </summary>
    public ClaimPolicyConfig? ClaimPolicy { get; set; }
    /// <summary>
    /// Helper to copy values from a user-supplied OidcOptions instance to the instance
    /// created by the framework inside AddOpenIdConnect().
    /// </summary>
    /// <param name="target">The target options to copy to.</param>
    public void ApplyTo(OidcOptions target)
    {
        ApplyTo((OpenIdConnectOptions)target);
        target.JwkJson = JwkJson;
        // OpenAPI / documentation properties
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DisplayName = DisplayName;
        target.DocumentationId = DocumentationId;
        target.Host = Host;
    }

    /// <summary>
    /// Helper to copy values from a user-supplied OpenIdConnectOptions instance to the instance
    /// created by the framework inside AddOpenIdConnect().
    /// </summary>
    /// <param name="target">The target options to copy to.</param>
    public void ApplyTo(OpenIdConnectOptions target)
    {
        CopyCoreEndpoints(target);
        CopyFlowConfiguration(target);
        CopyScopes(target);
        CopyTokenHandling(target);
        CopyPaths(target);
        CopyTokenValidation(target);
        CopySchemeLinkage(target);
        CopyBackchannelConfiguration(target);
        CopyConfiguration(target);
        CopyClaimActions(target);
        CopyEvents(target);
        CopyIssuerAndProperties(target);
#if NET9_0_OR_GREATER
        CopyNet9Features(target);
#endif
    }

    private void CopyCoreEndpoints(OpenIdConnectOptions target)
    {
        target.Authority = Authority;
        target.ClientId = ClientId;
        target.ClientSecret = ClientSecret;
    }

    private void CopyFlowConfiguration(OpenIdConnectOptions target)
    {
        target.ResponseType = ResponseType;
        target.ResponseMode = ResponseMode;
        target.UsePkce = UsePkce;
        target.RequireHttpsMetadata = RequireHttpsMetadata;
    }

    private void CopyScopes(OpenIdConnectOptions target)
    {
        target.Scope.Clear();
        foreach (var scope in Scope)
        {
            target.Scope.Add(scope);
        }
    }

    private void CopyTokenHandling(OpenIdConnectOptions target)
    {
        target.SaveTokens = SaveTokens;
        target.GetClaimsFromUserInfoEndpoint = GetClaimsFromUserInfoEndpoint;
        target.MapInboundClaims = MapInboundClaims;
        target.UseSecurityTokenValidator = UseSecurityTokenValidator;
    }

    private void CopyPaths(OpenIdConnectOptions target)
    {
        target.CallbackPath = CallbackPath;
        target.SignedOutCallbackPath = SignedOutCallbackPath;
        target.SignedOutRedirectUri = SignedOutRedirectUri;
        target.RemoteSignOutPath = RemoteSignOutPath;
    }

    private void CopyTokenValidation(OpenIdConnectOptions target)
    {
        if (TokenValidationParameters != null)
        {
            target.TokenValidationParameters = TokenValidationParameters;
        }
    }

    private void CopySchemeLinkage(OpenIdConnectOptions target)
    {
        target.SignInScheme = SignInScheme;
        target.SignOutScheme = SignOutScheme;
    }

    private void CopyBackchannelConfiguration(OpenIdConnectOptions target)
    {
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
    }

    private void CopyConfiguration(OpenIdConnectOptions target)
    {
        if (Configuration != null)
        {
            target.Configuration = Configuration;
        }
        if (ConfigurationManager != null)
        {
            target.ConfigurationManager = ConfigurationManager;
        }
    }

    private void CopyClaimActions(OpenIdConnectOptions target)
    {
        if (ClaimActions == null)
        {
            return;
        }
        foreach (var jka in ClaimActions
            .OfType<Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction>()
            .Where(a => !string.IsNullOrEmpty(a.JsonKey) && !string.IsNullOrEmpty(a.ClaimType)))
        {
            target.ClaimActions.MapJsonKey(jka.ClaimType, jka.JsonKey);
        }
    }

    private void CopyEvents(OpenIdConnectOptions target)
    {
        if (Events != null)
        {
            target.Events = Events;
        }
        if (EventsType != null)
        {
            target.EventsType = EventsType;
        }
    }

    private void CopyIssuerAndProperties(OpenIdConnectOptions target)
    {
        target.ClaimsIssuer = ClaimsIssuer;
        target.DisableTelemetry = DisableTelemetry;
        target.MaxAge = MaxAge;
        target.ProtocolValidator = ProtocolValidator;
        target.RefreshOnIssuerKeyNotFound = RefreshOnIssuerKeyNotFound;
        target.Resource = Resource;
        target.SkipUnrecognizedRequests = SkipUnrecognizedRequests;
        target.StateDataFormat = StateDataFormat;
        target.StringDataFormat = StringDataFormat;
    }

#if NET9_0_OR_GREATER
    private void CopyNet9Features(OpenIdConnectOptions target)
    {
        target.PushedAuthorizationBehavior = PushedAuthorizationBehavior;
        if (AdditionalAuthorizationParameters != null)
        {
            foreach (var param in AdditionalAuthorizationParameters)
            {
                target.AdditionalAuthorizationParameters[param.Key] = param.Value;
            }
        }
    }
#endif
}

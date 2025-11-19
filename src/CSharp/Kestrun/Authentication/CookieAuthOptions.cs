using System.Security.Claims;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Kestrun.Authentication;

/// <summary>
/// Options for cookie-based authentication.
/// </summary>
public class CookieAuthOptions : CookieAuthenticationOptions, IAuthenticationCommonOptions
{
    /// <summary>
    /// If true, allows cookie authentication over insecure HTTP connections.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>
    /// Optional display name for the authentication scheme in OpenAPI documentation.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <inheritdoc/>
    public Serilog.ILogger Logger { get; set; } = Serilog.Log.ForContext<ApiKeyAuthenticationOptions>();

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

    /// <inheritdoc/>
    public AuthenticationCodeSettings ValidateCodeSettings { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    /// <inheritdoc/>
    public Func<HttpContext, string, Task<IEnumerable<Claim>>>? IssueClaims { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    /// <inheritdoc/>
    public AuthenticationCodeSettings IssueClaimsCodeSettings { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    /// <inheritdoc/>
    public ClaimPolicyConfig? ClaimPolicyConfig { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    /// <summary>
    /// Helper to copy values from a user-supplied CookieAuthenticationOptions instance to the instance
    /// created by the framework inside AddCookie(). Reassigning the local variable (opts = source) would
    /// not work because only the local reference changes – the framework keeps the original instance.
    /// </summary>
    /// <param name="target">The target options to copy to.</param>
    /// <exception cref="ArgumentNullException">Thrown when source or target is null.</exception>
    /// <remarks>
    /// Only copies primitive properties and references. Does not clone complex objects like CookieBuilder.
    /// </remarks>
    public void ApplyTo(CookieAuthOptions target)
    {
        ApplyTo((CookieAuthenticationOptions)target);
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DocumentationId = DocumentationId;
    }


    /// <summary>
    /// Helper to copy values from this CookieAuthOptions instance to a target CookieAuthenticationOptions instance.
    /// </summary>
    /// <param name="target">The target CookieAuthenticationOptions instance to copy values to.</param>
    public void ApplyTo(CookieAuthenticationOptions target)
    {
        // Paths & return URL
        target.LoginPath = LoginPath;
        target.LogoutPath = LogoutPath;
        target.AccessDeniedPath = AccessDeniedPath;
        target.ReturnUrlParameter = ReturnUrlParameter;

        // Expiration & sliding behavior
        target.ExpireTimeSpan = ExpireTimeSpan;
        target.SlidingExpiration = SlidingExpiration;

        // Cookie builder settings
        // (Cookie is always non-null; copy primitive settings)
        if (Cookie.Name is not null)
        {
            target.Cookie.Name = Cookie.Name;
            target.Cookie.Path = Cookie.Path;
            target.Cookie.Domain = Cookie.Domain;
            target.Cookie.HttpOnly = Cookie.HttpOnly;
            target.Cookie.SameSite = Cookie.SameSite;
            target.Cookie.SecurePolicy = Cookie.SecurePolicy;
            target.Cookie.IsEssential = Cookie.IsEssential;
            target.Cookie.MaxAge = Cookie.MaxAge;
        }
        // Forwarding
        target.ForwardAuthenticate = ForwardAuthenticate;
        target.ForwardChallenge = ForwardChallenge;
        target.ForwardDefault = ForwardDefault;
        target.ForwardDefaultSelector = ForwardDefaultSelector;
        target.ForwardForbid = ForwardForbid;
        target.ForwardSignIn = ForwardSignIn;
        target.ForwardSignOut = ForwardSignOut;

        // Data protection / ticket / session
        target.TicketDataFormat = TicketDataFormat;
        target.DataProtectionProvider = DataProtectionProvider;
        target.SessionStore = SessionStore;

        // Events & issuer
        if (Events is not null)
        {
            target.Events = Events;
        }
        target.EventsType = EventsType;
        target.ClaimsIssuer = ClaimsIssuer;
    }



}

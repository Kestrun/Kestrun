
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kestrun.Authentication;

/// <summary>
/// Extension methods for <see cref="AuthenticationBuilder"/> to add cookie authentication.
/// </summary>
internal static class AuthenticationBuilderExtensions
{
    /// <summary>
    /// Adds cookie authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
    /// <para>
    /// Cookie authentication uses a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="displayName">A display name for the authentication handler.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="CookieAuthenticationOptions"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    public static AuthenticationBuilder AddCookie(this AuthenticationBuilder builder, string authenticationScheme, string? displayName, Action<CookieAuthOptions> configureOptions)
    {
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IPostConfigureOptions<CookieAuthenticationOptions>,
                PostConfigureCookieAuthenticationOptions>());

        _ = builder.Services
            .AddOptions<CookieAuthenticationOptions>(authenticationScheme)
            .Validate(o => o.Cookie.Expiration == null,
                "Cookie.Expiration is ignored, use ExpireTimeSpan instead.");

        return builder.AddScheme<CookieAuthenticationOptions, CookieAuthenticationHandler>(authenticationScheme, displayName, options =>
            {
                if (configureOptions is null)
                {
                    return;
                }

                // Start from a fresh Kestrun-friendly options object
                var userOptions = new CookieAuthOptions();

                // Let the caller configure all the rich stuff
                configureOptions(userOptions);

                // Push everything into the real framework options
                userOptions.ApplyTo(options);
            });
    }

}

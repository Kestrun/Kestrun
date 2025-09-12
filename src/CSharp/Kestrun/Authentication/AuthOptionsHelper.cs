using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kestrun.Authentication;

/// <summary>
/// Helper class to retrieve authentication options from the DI container.
/// </summary>
public static class AuthOptionsHelper
{
    /// <summary>
    /// Retrieve authentication options from the DI container.
    /// </summary>
    /// <typeparam name="TOptions">The type of authentication options.</typeparam>
    /// <param name="ctx">The HTTP context.</param>
    /// <param name="scheme">The authentication scheme.</param>
    /// <returns>The authentication options for the specified scheme.</returns>
    public static TOptions GetAuthOptions<TOptions>(HttpContext ctx, string scheme)
        where TOptions : AuthenticationSchemeOptions
    {
        var monitor = ctx.RequestServices
            .GetRequiredService<IOptionsMonitor<TOptions>>();

        return monitor.Get(scheme);
    }
}


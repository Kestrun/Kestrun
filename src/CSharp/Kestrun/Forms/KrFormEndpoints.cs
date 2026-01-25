using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kestrun.Forms;

/// <summary>
/// Extension methods for registering Kestrun form routes.
/// </summary>
public static class KrFormEndpoints
{
    /// <summary>
    /// Maps a Kestrun form parsing route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="options">The form parsing options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="handler">The handler function to invoke after parsing (receives KrFormContext).</param>
    /// <returns>The route handler builder for further configuration.</returns>
    public static RouteHandlerBuilder MapKestrunFormRoute(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        KrFormOptions options,
        Serilog.ILogger logger,
        Func<KrFormContext, Task<IResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(handler);

        return endpoints.MapPost(pattern, async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            // Parse the form
            var formContext = await KrFormParser.ParseAsync(httpContext, options, logger, cancellationToken);

            // Call OnCompleted hook if provided
            if (options.OnCompleted != null)
            {
                var shortCircuitResult = await options.OnCompleted(formContext);
                if (shortCircuitResult != null)
                {
                    logger.Information("Form parsing short-circuited by OnCompleted hook");
                    return Results.Ok(shortCircuitResult);
                }
            }

            // Call the user handler
            return await handler(formContext);
        })
        .DisableAntiforgery(); // Form parsing routes don't need antiforgery by default
    }

    /// <summary>
    /// Maps a Kestrun form parsing route with default options.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="handler">The handler function to invoke after parsing (receives KrFormContext).</param>
    /// <returns>The route handler builder for further configuration.</returns>
    public static RouteHandlerBuilder MapKestrunFormRoute(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Serilog.ILogger logger,
        Func<KrFormContext, Task<IResult>> handler)
    {
        return MapKestrunFormRoute(endpoints, pattern, new KrFormOptions(), logger, handler);
    }
}

using Kestrun.Hosting;
using Kestrun.Models;

namespace Kestrun.Forms;

/// <summary>
/// Provides endpoint mapping helpers for form routes.
/// </summary>
public static class KrFormEndpoints
{
    /// <summary>
    /// Represents a delegate that handles a parsed form context.
    /// </summary>
    /// <param name="context">The form context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public delegate Task KrFormHandler(KrFormContext context);

    /// <summary>
    /// Maps a form route that parses multipart and urlencoded payloads.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="options">The form parsing options.</param>
    /// <param name="handler">The handler to execute after parsing.</param>
    /// <returns>The endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapKestrunFormRoute(this IEndpointRouteBuilder endpoints, string pattern, KrFormOptions options, KrFormHandler handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        var map = endpoints.MapPost(pattern, async httpContext =>
        {
            var host = httpContext.RequestServices.GetRequiredService<KestrunHost>();
            var kestrunContext = new KestrunContext(host, httpContext);
            options.Logger ??= host.Logger;

            try
            {
                var payload = await KrFormParser.ParseAsync(httpContext, options, httpContext.RequestAborted).ConfigureAwait(false);
                var formContext = new KrFormContext(kestrunContext, options, payload);

                httpContext.Items["KrFormContext"] = formContext;
                httpContext.Items["KrFormPayload"] = payload;

                if (options.OnCompleted != null)
                {
                    var result = await options.OnCompleted(formContext).ConfigureAwait(false);
                    if (result != null)
                    {
                        await Results.Ok(result).ExecuteAsync(httpContext).ConfigureAwait(false);
                        return;
                    }
                }

                await handler(formContext).ConfigureAwait(false);
                await kestrunContext.Response.ApplyTo(httpContext.Response).ConfigureAwait(false);
            }
            catch (KrFormException ex)
            {
                host.Logger.Error(ex, "Form parsing failed for {Pattern}", pattern);
                httpContext.Response.StatusCode = ex.StatusCode;
                await httpContext.Response.WriteAsync("Invalid form data.", httpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                host.Logger.Error(ex, "Unhandled error in form route for {Pattern}", pattern);
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsync("Internal server error.", httpContext.RequestAborted).ConfigureAwait(false);
            }
        });

        _ = map.DisableAntiforgery();
        return map;
    }
}

using Kestrun.Sse;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using System.Text.Json;
using System.Threading.Channels;

namespace Kestrun.Hosting;

/// <summary>
/// Extension methods for <see cref="KestrunHost"/> to support SSE broadcast connections and broadcasting.
/// </summary>
public static class KestrunHostSseExtensions
{
    /// <summary>
    /// Adds an SSE broadcast endpoint that keeps a connection open and streams broadcast events.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="path">The URL path for the SSE broadcast connection endpoint.</param>
    /// <param name="keepAliveSeconds">If greater than 0, sends periodic SSE comments to keep intermediaries from timing out the connection.</param>
    /// <returns>The host instance.</returns>
    public static KestrunHost AddSseBroadcast(this KestrunHost host, string path = "/sse/broadcast", int keepAliveSeconds = 15)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (!host.MapExists(path, HttpVerb.Get))
        {
            RegisterSseBroadcastRouteForOpenApi(host, path);
        }

        return host
            .AddService(services =>
            {
                services.TryAddSingleton(_ => host.Logger);
                services.TryAddSingleton<ISseBroadcaster, InMemorySseBroadcaster>();
            })
            .Use(app => ((IEndpointRouteBuilder)app).MapGet(path, httpContext => HandleSseBroadcastConnectAsync(host, httpContext, keepAliveSeconds)));
    }

    /// <summary>
    /// Registers the broadcast SSE endpoint in the host's route registry with OpenAPI metadata.
    /// This allows the OpenAPI generator to include the endpoint even though it is mapped directly
    /// through ASP.NET Core endpoint routing (not via <c>AddMapRoute</c>).
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="path">The broadcast SSE endpoint path.</param>
    private static void RegisterSseBroadcastRouteForOpenApi(KestrunHost host, string path)
    {
        var routeOptions = new MapRouteOptions
        {
            Pattern = path,
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native,
                Code = string.Empty
            }
        };

        var meta = new OpenAPIPathMetadata(pattern: path, mapOptions: routeOptions)
        {
            OperationId = "GetSseBroadcast",
            Summary = "Broadcast SSE stream",
            Description = "Opens a Server-Sent Events (text/event-stream) connection that receives server-side broadcast events.",
            Tags = ["SSE"],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "SSE stream (text/event-stream)",
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["text/event-stream"] = new OpenApiMediaType
                        {
                            ItemSchema = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            }
        };

        routeOptions.OpenAPI[HttpVerb.Get] = meta;
        host._registeredRoutes[(path, HttpVerb.Get)] = routeOptions;
    }

    /// <summary>
    /// Gets the number of currently connected SSE broadcast clients, if available.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <returns>Connected client count, or null when the app is not built or SSE broadcaster is not registered.</returns>
    public static int? GetSseConnectedClientCount(this KestrunHost host)
    {
        try
        {
            var svcProvider = host.App?.Services;
            return svcProvider?.GetService(typeof(ISseBroadcaster)) is ISseBroadcaster b ? b.ConnectedCount : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Broadcasts an SSE event to all connected clients.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="eventName">Event name (optional).</param>
    /// <param name="data">Event payload.</param>
    /// <param name="id">Optional event ID.</param>
    /// <param name="retryMs">Optional reconnect interval in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when broadcast succeeded (or no clients); false when broadcaster isn't configured.</returns>
    public static async Task<bool> BroadcastSseEventAsync(this KestrunHost host, string? eventName, string data, string? id = null, int? retryMs = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var svcProvider = host.App?.Services;
            if (svcProvider == null)
            {
                host.Logger.Warning("No service provider available to resolve ISseBroadcaster.");
                return false;
            }

            if (svcProvider.GetService(typeof(ISseBroadcaster)) is not ISseBroadcaster broadcaster)
            {
                host.Logger.Warning("ISseBroadcaster service is not registered. Make sure SSE broadcast is configured.");
                return false;
            }

            await broadcaster.BroadcastAsync(eventName, data, id, retryMs, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (InvalidOperationException)
        {
            host.Logger.Warning("WebApplication is not built yet. Call Build() first.");
            return false;
        }
        catch (Exception ex)
        {
            host.Logger.Error(ex, "Failed to broadcast SSE event: {EventName}", eventName);
            return false;
        }
    }

    /// <summary>
    /// Handles a broadcast SSE connection (server keeps response open and streams broadcast events).
    /// </summary>
    /// <param name="host">Kestrun host.</param>
    /// <param name="httpContext">ASP.NET Core HTTP context.</param>
    /// <param name="keepAliveSeconds">Keep-alive interval in seconds.</param>
    private static async Task HandleSseBroadcastConnectAsync(KestrunHost host, HttpContext httpContext, int keepAliveSeconds)
    {
        if (httpContext.RequestServices.GetService(typeof(ISseBroadcaster)) is not ISseBroadcaster broadcaster)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsync("SSE broadcaster is not configured.", httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        var ctx = new Models.KestrunContext(host, httpContext);
        ctx.StartSse();

        var subscription = broadcaster.Subscribe(httpContext.RequestAborted);

        var connectedJson = JsonSerializer.Serialize(new
        {
            clientId = subscription.ClientId,
            serverTime = DateTimeOffset.UtcNow
        });

        await ctx.WriteSseEventAsync("connected", connectedJson, id: null, retryMs: 2000, ct: httpContext.RequestAborted).ConfigureAwait(false);
        await PumpSseAsync(httpContext, subscription.Reader, keepAliveSeconds).ConfigureAwait(false);
    }

    /// <summary>
    /// Pumps formatted SSE payloads from a channel to the HTTP response, with optional keep-alives.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="reader">Channel reader.</param>
    /// <param name="keepAliveSeconds">Keep-alive interval in seconds.</param>
    private static async Task PumpSseAsync(HttpContext httpContext, ChannelReader<string> reader, int keepAliveSeconds)
    {
        var ct = httpContext.RequestAborted;
        var keepAliveTask = keepAliveSeconds > 0
            ? Task.Delay(TimeSpan.FromSeconds(keepAliveSeconds), ct)
            : null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var readTask = reader.ReadAsync(ct).AsTask();

                if (keepAliveTask == null)
                {
                    var payload = await readTask.ConfigureAwait(false);
                    await WritePayloadAsync(httpContext, payload, ct).ConfigureAwait(false);
                    continue;
                }

                var completed = await Task.WhenAny(readTask, keepAliveTask).ConfigureAwait(false);

                if (completed == readTask)
                {
                    var payload = await readTask.ConfigureAwait(false);
                    await WritePayloadAsync(httpContext, payload, ct).ConfigureAwait(false);
                }
                else
                {
                    await WritePayloadAsync(httpContext, SseEventFormatter.FormatComment($"keep-alive {DateTimeOffset.UtcNow:O}"), ct).ConfigureAwait(false);
                    keepAliveTask = Task.Delay(TimeSpan.FromSeconds(keepAliveSeconds), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected.
        }
        catch (ChannelClosedException)
        {
            // Server removed the client.
        }
        finally
        {
            // No-op; keepAliveTask is GC'd.
        }
    }

    /// <summary>
    /// Writes a pre-formatted SSE payload string to the response and flushes.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="payload">Pre-formatted payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static async Task WritePayloadAsync(HttpContext httpContext, string payload, CancellationToken cancellationToken)
    {
        await httpContext.Response.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

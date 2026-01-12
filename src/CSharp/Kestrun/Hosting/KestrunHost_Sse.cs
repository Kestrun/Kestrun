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
/// SSE broadcast extensions
/// </summary>
public partial class KestrunHost
{
    /// <summary>
    /// Adds an SSE broadcast endpoint that keeps a connection open and streams broadcast events.
    /// </summary>
    /// <param name="options">Optional OpenAPI customization for the broadcast endpoint route registry entry.</param>
    /// <returns>The host instance.</returns>
    public KestrunHost AddSseBroadcast(SseBroadcastOptions options)
    {
        // Register immediately so OpenAPI generation and callers can see the route metadata
        // without requiring Build()/Use() to have executed.
        RegisterSseBroadcastRouteForOpenApi(options);

        return AddService(services =>
            {
                services.TryAddSingleton(_ => Logger);
                services.TryAddSingleton<ISseBroadcaster, InMemorySseBroadcaster>();
            })
            .Use(app =>
            {
                _ = ((IEndpointRouteBuilder)app).MapGet(options.Path, httpContext => HandleSseBroadcastConnectAsync(httpContext, options.KeepAliveSeconds));
            });
    }

    /// <summary>
    /// Registers the broadcast SSE endpoint in the host's route registry with OpenAPI metadata.
    /// This allows the OpenAPI generator to include the endpoint even though it is mapped directly
    /// through ASP.NET Core endpoint routing (not via <c>AddMapRoute</c>).
    /// </summary>
    /// <param name="options">Optional OpenAPI customization.</param>
    private void RegisterSseBroadcastRouteForOpenApi(SseBroadcastOptions? options)
    {
        options ??= SseBroadcastOptions.Default;
        // Ensure the OpenAPI document descriptor exists (SSE broadcast can be configured even when OpenAPI is not).
        var docs = GetOrCreateOpenApiDocument(options.DocId);
        var routeOptions = new MapRouteOptions
        {
            Pattern = options.Path,
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native,
                Code = string.Empty
            }
        };

        if (!options.SkipOpenApi)
        {
            var mediaType = new OpenApiMediaType
            {
                ItemSchema = docs[0].InferPrimitiveSchema(options.ItemSchemaType)
            };

            var meta = new OpenAPIPathMetadata(pattern: options.Path, mapOptions: routeOptions)
            {
                OperationId = string.IsNullOrWhiteSpace(options.OperationId) ? null : options.OperationId,
                Summary = string.IsNullOrWhiteSpace(options.Summary) ? null : options.Summary,
                Description = string.IsNullOrWhiteSpace(options.Description) ? null : options.Description,
                Tags = options.Tags?.ToList() ?? [],
                Responses = new OpenApiResponses
                {
                    [options.StatusCode] = new OpenApiResponse
                    {
                        Description = string.IsNullOrWhiteSpace(options.ResponseDescription)
                            ? "SSE stream (text/event-stream)"
                            : options.ResponseDescription,
                        Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                        {
                            [options.ContentType] = mediaType
                        }
                    }
                }
            };

            routeOptions.OpenAPI[HttpVerb.Get] = meta;
        }
        _registeredRoutes[(options.Path, HttpVerb.Get)] = routeOptions;
    }

    /// <summary>
    /// Gets the number of currently connected SSE broadcast clients, if available.
    /// </summary>
    /// <returns>Connected client count, or null when the app is not built or SSE broadcaster is not registered.</returns>
    public int? GetSseConnectedClientCount()
    {
        try
        {
            var svcProvider = App?.Services;
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
    /// <param name="eventName">Event name (optional).</param>
    /// <param name="data">Event payload.</param>
    /// <param name="id">Optional event ID.</param>
    /// <param name="retryMs">Optional reconnect interval in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when broadcast succeeded (or no clients); false when broadcaster isn't configured.</returns>
    public async Task<bool> BroadcastSseEventAsync(string? eventName, string data, string? id = null, int? retryMs = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var svcProvider = App?.Services;
            if (svcProvider == null)
            {
                Logger.Warning("No service provider available to resolve ISseBroadcaster.");
                return false;
            }

            if (svcProvider.GetService(typeof(ISseBroadcaster)) is not ISseBroadcaster broadcaster)
            {
                Logger.Warning("ISseBroadcaster service is not registered. Make sure SSE broadcast is configured.");
                return false;
            }

            await broadcaster.BroadcastAsync(eventName, data, id, retryMs, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (InvalidOperationException)
        {
            Logger.Warning("WebApplication is not built yet. Call Build() first.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to broadcast SSE event: {EventName}", eventName);
            return false;
        }
    }

    /// <summary>
    /// Handles a broadcast SSE connection (server keeps response open and streams broadcast events).
    /// </summary>
    /// <param name="httpContext">ASP.NET Core HTTP context.</param>
    /// <param name="keepAliveSeconds">Keep-alive interval in seconds.</param>
    private async Task HandleSseBroadcastConnectAsync(HttpContext httpContext, int keepAliveSeconds)
    {
        if (httpContext.RequestServices.GetService(typeof(ISseBroadcaster)) is not ISseBroadcaster broadcaster)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsync("SSE broadcaster is not configured.", httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        var ctx = new Models.KestrunContext(this, httpContext);
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
    private async Task PumpSseAsync(HttpContext httpContext, ChannelReader<string> reader, int keepAliveSeconds)
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
    private async Task WritePayloadAsync(HttpContext httpContext, string payload, CancellationToken cancellationToken)
    {
        await httpContext.Response.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

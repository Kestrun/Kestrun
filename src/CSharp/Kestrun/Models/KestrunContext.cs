

using System.Collections;
using System.Security.Claims;
using Kestrun.Hosting.Options;
using Kestrun.SignalR;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;

namespace Kestrun.Models;

/// <summary>
/// Represents the context for a Kestrun request, including the request, response, HTTP context, and host.
/// </summary>
public sealed record KestrunContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunContext"/> class.
    /// This constructor is used when creating a new KestrunContext from an existing HTTP context.
    /// It initializes the KestrunRequest and KestrunResponse based on the provided HttpContext
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="httpContext">The associated HTTP context.</param>
    public KestrunContext(Hosting.KestrunHost host, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(httpContext);

        Host = host;
        HttpContext = httpContext;
        // Initialize TraceIdentifier, Request, and Response
        TraceIdentifier = HttpContext.TraceIdentifier;
        Request = KestrunRequest.NewRequestSync(HttpContext);

        // Ensure contexts created via this constructor always have a valid response.
        Response = new KestrunResponse(this, 8192);
        // Routing metadata may not always be available (e.g., middleware/tests/exception handlers).
        // Fall back to the request path if no RouteEndpoint is present.
        var routeEndpoint = Request.HttpContext.GetEndpoint() as RouteEndpoint;

        var pattern = routeEndpoint?.RoutePattern.RawText;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "/";
        }

        var verb = string.IsNullOrWhiteSpace(Request.Method)
            ? HttpVerb.Get
            : HttpVerbExtensions.FromMethodString(Request.Method);

        if (!Host.RegisteredRoutes.TryGetValue((pattern, verb), out var options))
        {
            // default options
            options = new MapRouteOptions()
            {
                Pattern = pattern,
                HttpVerbs = [verb]
            };
        }

        MapRouteOptions = options;
    }

    /// <summary>
    /// The Kestrun host associated with this context.
    /// </summary>
    public Hosting.KestrunHost Host { get; init; }

    /// <summary>
    /// The logger associated with the Kestrun host.
    /// </summary>
    public Serilog.ILogger Logger => Host.Logger;
    /// <summary>
    /// The Kestrun request associated with this context.
    /// </summary>
    public KestrunRequest Request { get; init; }
    /// <summary>
    /// The Kestrun response associated with this context.
    /// </summary>
    public KestrunResponse Response { get; private set; }
    /// <summary>
    /// The ASP.NET Core HTTP context associated with this Kestrun context.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets the route options associated with this response.
    /// </summary>
    public MapRouteOptions MapRouteOptions { get; init; }
    /// <summary>
    /// Returns the ASP.NET Core session if the Session middleware is active; otherwise null.
    /// </summary>
    public ISession? Session => HttpContext.Features.Get<ISessionFeature>()?.Session;

    /// <summary>
    /// True if Session middleware is active for this request.
    /// </summary>
    public bool HasSession => Session is not null;

    /// <summary>
    /// Try pattern to get session without exceptions.
    /// </summary>
    public bool TryGetSession(out ISession? session)
    {
        session = Session;
        return session is not null;
    }

    /// <summary>
    /// Gets the cancellation token that is triggered when the HTTP request is aborted.
    /// </summary>
    public CancellationToken Ct => HttpContext.RequestAborted;
    /// <summary>
    /// Gets the collection of key/value pairs associated with the current HTTP context.
    /// </summary>
    public IDictionary<object, object?> Items => HttpContext.Items;

    /// <summary>
    /// Gets the user associated with the current HTTP context.
    /// </summary>
    public ClaimsPrincipal User => HttpContext.User;

    /// <summary>
    /// Gets the connection information for the current HTTP context.
    /// </summary>
    public ConnectionInfo Connection => HttpContext.Connection;

    /// <summary>
    /// Gets the trace identifier for the current HTTP context.
    /// </summary>
    public string TraceIdentifier { get; init; }

    /// <summary>
    /// A dictionary to hold  parameters passed by user for use within the KestrunContext.
    /// </summary>
    public ResolvedRequestParameters Parameters { get; internal set; } = new ResolvedRequestParameters();

    /// <summary>
    /// Returns a string representation of the KestrunContext, including path, user, and session status.
    /// </summary>
    public override string ToString()
        => $"KestrunContext{{ Host={Host}, Path={HttpContext.Request.Path}, User={User?.Identity?.Name ?? "<anon>"}, HasSession={HasSession} }}";

    /// <summary>
    /// Asynchronously broadcasts a log message to all connected SignalR clients using the IRealtimeBroadcaster service.
    /// </summary>
    /// <param name="level">The log level (e.g., Information, Warning, Error, Debug, Verbose).</param>
    /// <param name="message">The log message to broadcast.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    /// <returns>True if the log was broadcast successfully; otherwise, false.</returns>
    public async Task<bool> BroadcastLogAsync(string level, string message, CancellationToken cancellationToken = default)
    {
        var svcProvider = HttpContext.RequestServices;

        if (svcProvider == null)
        {
            Logger.Warning("No service provider available to resolve IRealtimeBroadcaster.");
            return false;
        }
        if (svcProvider.GetService(typeof(IRealtimeBroadcaster)) is not IRealtimeBroadcaster broadcaster)
        {
            Logger.Warning("IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.");
            return false;
        }
        try
        {
            await broadcaster.BroadcastLogAsync(level, message, cancellationToken);
            Logger.Debug("Broadcasted log message via SignalR: {Level} - {Message}", level, message);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to broadcast log message: {Level} - {Message}", level, message);
            return false;
        }
    }

    /// <summary>
    /// Synchronous wrapper for BroadcastLogAsync.
    /// </summary>
    /// <param name="level">The log level (e.g., Information, Warning, Error, Debug, Verbose).</param>
    /// <param name="message">The log message to broadcast.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    /// <returns>True if the log was broadcast successfully; otherwise, false.</returns>
    public bool BroadcastLog(string level, string message, CancellationToken cancellationToken = default) =>
        BroadcastLogAsync(level, message, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously broadcasts a custom event to all connected SignalR clients using the IRealtimeBroadcaster service.
    /// </summary>
    /// <param name="eventName">The event name (e.g., Information, Warning, Error, Debug, Verbose).</param>
    /// <param name="data">The event data to broadcast.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    /// <returns>True if the event was broadcast successfully; otherwise, false.</returns>
    public async Task<bool> BroadcastEventAsync(string eventName, object? data, CancellationToken cancellationToken = default)
    {
        var svcProvider = HttpContext.RequestServices;

        if (svcProvider == null)
        {
            Logger.Warning("No service provider available to resolve IRealtimeBroadcaster.");
            return false;
        }
        if (svcProvider.GetService(typeof(IRealtimeBroadcaster)) is not IRealtimeBroadcaster broadcaster)
        {
            Logger.Warning("IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.");
            return false;
        }
        try
        {
            await broadcaster.BroadcastEventAsync(eventName, data, cancellationToken);
            Logger.Debug("Broadcasted event via SignalR: {EventName} - {Data}", eventName, data);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to broadcast event: {EventName} - {Data}", eventName, data);
            return false;
        }
    }

    /// <summary>
    /// Synchronous wrapper for BroadcastEventAsync.
    /// </summary>
    /// <param name="eventName">The event name (e.g., Information, Warning, Error, Debug, Verbose).</param>
    /// <param name="data">The event data to broadcast.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    /// <returns>True if the event was broadcast successfully; otherwise, false.</returns>
    public bool BroadcastEvent(string eventName, object? data, CancellationToken cancellationToken = default) =>
      BroadcastEventAsync(eventName, data, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously broadcasts a message to a specific group of SignalR clients using the IRealtimeBroadcaster service.
    /// </summary>
    /// <param name="groupName">The name of the group to broadcast the message to.</param>
    /// <param name="method">The name of the method to invoke on the client.</param>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    /// <returns></returns>
    public async Task<bool> BroadcastToGroupAsync(string groupName, string method, object? message, CancellationToken cancellationToken = default)
    {
        var svcProvider = HttpContext.RequestServices;

        if (svcProvider == null)
        {
            Logger.Warning("No service provider available to resolve IRealtimeBroadcaster.");
            return false;
        }
        if (svcProvider.GetService(typeof(IRealtimeBroadcaster)) is not IRealtimeBroadcaster broadcaster)
        {
            Logger.Warning("IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.");
            return false;
        }
        try
        {
            await broadcaster.BroadcastToGroupAsync(groupName, method, message, cancellationToken);
            Logger.Debug("Broadcasted log message to group via SignalR: {GroupName} - {Method} - {Message}", groupName, method, message);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to broadcast log message: {GroupName} - {Method} - {Message}", groupName, method, message);
            return false;
        }
    }

    /// <summary>
    /// Synchronous wrapper for BroadcastToGroupAsync.
    /// </summary>
    /// <param name="groupName">The name of the group to broadcast the message to.</param>
    /// <param name="method">The name of the method to invoke on the client.</param>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    /// <returns></returns>
    public bool BroadcastToGroup(string groupName, string method, object? message, CancellationToken cancellationToken = default) =>
      BroadcastToGroupAsync(groupName, method, message, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Synchronous wrapper for HttpContext.ChallengeAsync.
    /// </summary>
    /// <param name="scheme">The authentication scheme to challenge.</param>
    /// <param name="properties">The authentication properties to include in the challenge.</param>
    public void Challenge(string? scheme, AuthenticationProperties? properties) => HttpContext.ChallengeAsync(scheme, properties).GetAwaiter().GetResult();

    /// <summary>
    /// Synchronous wrapper for HttpContext.ChallengeAsync using a Hashtable for properties.
    /// </summary>
    /// <param name="scheme">The authentication scheme to challenge.</param>
    /// <param name="properties">The authentication properties to include in the challenge.</param>
    public void Challenge(string? scheme, Hashtable? properties)
    {
        var dict = new Dictionary<string, string?>();
        if (properties != null)
        {
            foreach (DictionaryEntry entry in properties)
            {
                dict[entry.Key.ToString()!] = entry.Value?.ToString();
            }
        }
        AuthenticationProperties authProps = new(dict);
        HttpContext.ChallengeAsync(scheme, authProps).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous wrapper for HttpContext.ChallengeAsync using a Dictionary for properties.
    /// </summary>
    /// <param name="scheme">The authentication scheme to challenge.</param>
    /// <param name="properties">The authentication properties to include in the challenge.</param>
    public void Challenge(string? scheme, Dictionary<string, string?>? properties)
    {
        if (properties == null)
        {
            HttpContext.ChallengeAsync(scheme).GetAwaiter().GetResult();
            return;
        }

        AuthenticationProperties authProps = new(properties);
        HttpContext.ChallengeAsync(scheme, authProps).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronous wrapper for HttpContext.ChallengeAsync using a Hashtable for properties.
    /// </summary>
    /// <param name="scheme">The authentication scheme to challenge.</param>
    /// <param name="properties">The authentication properties to include in the challenge.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task ChallengeAsync(string? scheme, Hashtable? properties)
    {
        var dict = new Dictionary<string, string?>();
        if (properties != null)
        {
            foreach (DictionaryEntry entry in properties)
            {
                dict[entry.Key.ToString()!] = entry.Value?.ToString();
            }
        }
        AuthenticationProperties authProps = new(dict);
        return HttpContext.ChallengeAsync(scheme, authProps);
    }

    /// <summary>
    /// Synchronous wrapper for HttpContext.SignOutAsync.
    /// </summary>
    /// <param name="scheme">The authentication scheme to sign out.</param>
    public void SignOut(string? scheme) => HttpContext.SignOutAsync(scheme).GetAwaiter().GetResult();
    /// <summary>
    /// Synchronous wrapper for HttpContext.SignOutAsync.
    /// </summary>
    /// <param name="scheme">The authentication scheme to sign out.</param>
    /// <param name="properties">The authentication properties to include in the sign-out.</param>
    public void SignOut(string? scheme, AuthenticationProperties? properties)
    {
        HttpContext.SignOutAsync(scheme, properties).GetAwaiter().GetResult();
        if (properties != null && !string.IsNullOrWhiteSpace(properties.RedirectUri))
        {
            Response.WriteStatusOnly(302);
        }
    }

    /// <summary>
    /// Synchronous wrapper for HttpContext.SignOutAsync using a Hashtable for properties.
    /// </summary>
    /// <param name="scheme">The authentication scheme to sign out.</param>
    /// <param name="properties">The authentication properties to include in the sign-out.</param>
    public void SignOut(string? scheme, Hashtable? properties)
    {
        AuthenticationProperties? authProps = null;
        // Convert Hashtable to Dictionary<string, string?> for AuthenticationProperties
        if (properties is not null)
        {
            var dict = new Dictionary<string, string?>();
            // Convert each entry in the Hashtable to a string key-value pair
            foreach (DictionaryEntry entry in properties)
            {
                dict[entry.Key.ToString()!] = entry.Value?.ToString();
            }
            // Create AuthenticationProperties from the dictionary
            authProps = new AuthenticationProperties(dict);
        }
        // Call SignOut with the constructed AuthenticationProperties
        SignOut(scheme, authProps);
    }
}

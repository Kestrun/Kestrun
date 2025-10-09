

using System.Security.Claims;
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
    /// <param name="request">The Kestrun request.</param>
    /// <param name="response">The Kestrun response.</param>
    /// <param name="httpContext">The associated HTTP context.</param>
    public KestrunContext(Hosting.KestrunHost host, KestrunRequest request, KestrunResponse response, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(httpContext);

        Host = host;
        Request = request;
        Response = response;
        HttpContext = httpContext;

    }

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

        Request = KestrunRequest.NewRequestSync(HttpContext);
        Response = new KestrunResponse(Request);
    }

    /// <summary>
    /// The Kestrun host associated with this context.
    /// </summary>
    public Hosting.KestrunHost Host { get; init; }
    /// <summary>
    /// The Kestrun request associated with this context.
    /// </summary>
    public KestrunRequest Request { get; init; }
    /// <summary>
    /// The Kestrun response associated with this context.
    /// </summary>
    public KestrunResponse Response { get; init; }
    /// <summary>
    /// The ASP.NET Core HTTP context associated with this Kestrun context.
    /// </summary>
    public HttpContext HttpContext { get; init; }
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
    /// Returns a string representation of the KestrunContext, including path, user, and session status.
    /// </summary>
    public override string ToString()
        => $"KestrunContext{{ Host={Host}, Path={HttpContext.Request.Path}, User={User?.Identity?.Name ?? "<anon>"}, HasSession={HasSession} }}";
}

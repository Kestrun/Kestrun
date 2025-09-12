

using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;

namespace Kestrun.Models;

/// <summary>
/// Represents the context for a Kestrun request, including the request, response, HTTP context, and host.
/// </summary>
/// <param name="Request">The Kestrun request.</param>
/// <param name="Response">The Kestrun response.</param>
/// <param name="HttpContext">The associated HTTP context.</param>
public sealed record KestrunContext(KestrunRequest Request, KestrunResponse Response, HttpContext HttpContext)
{
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
        => $"KestrunContext{{ Path={HttpContext.Request.Path}, User={User?.Identity?.Name ?? "<anon>"}, HasSession={HasSession} }}";
}

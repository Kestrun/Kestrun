using System.Collections.ObjectModel;
using Kestrun.Hosting;
using Kestrun.Models;

namespace Kestrun.Forms;
/// <summary>
/// Represents the form parsing context for a request.
/// </summary>
public sealed record KrFormContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrFormContext"/> class.
    /// </summary>
    /// <param name="kestrunContext">The Kestrun context.</param>
    /// <param name="options">The form parsing options.</param>
    /// <param name="payload">The parsed payload.</param>
    public KrFormContext(KestrunContext kestrunContext, KrFormOptions options, KrFormPayload payload)
    {
        KestrunContext = kestrunContext;
        Host = kestrunContext.Host;
        HttpContext = kestrunContext.HttpContext;
        Options = options;
        Payload = payload;
    }

    /// <summary>
    /// Gets the associated Kestrun host.
    /// </summary>
    public KestrunHost Host { get; }

    /// <summary>
    /// Gets the associated Kestrun context.
    /// </summary>
    public KestrunContext KestrunContext { get; }

    /// <summary>
    /// Gets the underlying HTTP context.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// Gets the form parsing options.
    /// </summary>
    public KrFormOptions Options { get; }

    /// <summary>
    /// Gets the parsed payload.
    /// </summary>
    public KrFormPayload Payload { get; }

    /// <summary>
    /// Gets the logger associated with the host.
    /// </summary>
    public Serilog.ILogger Logger => Host.Logger;
}

/// <summary>
/// Represents the context for a part as it is being processed.
/// </summary>
public sealed record KrPartContext
{
    /// <summary>
    /// Gets the zero-based index of the part within the multipart body.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets the part name, if present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the original filename, if present.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets the content type of the part.
    /// </summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>
    /// Gets the content encoding of the part, if present.
    /// </summary>
    public string? ContentEncoding { get; init; }

    /// <summary>
    /// Gets the declared length of the part, if present.
    /// </summary>
    public long? DeclaredLength { get; init; }

    /// <summary>
    /// Gets the headers associated with the part.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; init; } = new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the matching rule for the part, if any.
    /// </summary>
    public KrFormPartRule? Rule { get; init; }
}

/// <summary>
/// Defines the action taken for a part.
/// </summary>
public enum KrPartAction
{
    /// <summary>
    /// Continue processing the part.
    /// </summary>
    Continue,

    /// <summary>
    /// Skip the part content without storing.
    /// </summary>
    Skip,

    /// <summary>
    /// Reject the request.
    /// </summary>
    Reject
}

/// <summary>
/// Represents form parsing errors with HTTP status codes.
/// </summary>
public class KrFormException(string message, int statusCode) : InvalidOperationException(message)
{
    /// <summary>
    /// Gets the HTTP status code to return.
    /// </summary>
    public int StatusCode { get; } = statusCode;
}

/// <summary>
/// Represents a form parsing limit violation.
/// </summary>
public sealed class KrFormLimitExceededException(string message, int statusCode = StatusCodes.Status413PayloadTooLarge)
    : KrFormException(message, statusCode)
{ }

namespace Kestrun.KrException;

/// <summary>
/// Exception thrown when there is an error parsing the Accept header from the client.
/// The StatusCode property indicates the HTTP status code that should be returned to the client.
/// </summary>
/// <param name="message">The error message describing the issue.</param>
/// <param name="statusCode">The HTTP status code to return.</param>
public class AcceptHeaderException(string message, int statusCode) : InvalidOperationException(message)
{
    /// <summary>
    /// Gets the HTTP status code to return.
    /// </summary>
    public int StatusCode { get; } = statusCode;
}

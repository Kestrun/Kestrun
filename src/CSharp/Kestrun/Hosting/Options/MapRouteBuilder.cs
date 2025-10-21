namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for mapping a route, including pattern, HTTP verbs, script code, authorization, and metadata.
/// </summary>
public record MapRouteBuilder : MapRouteOptions
{

    /// <summary>
    /// Initializes a new instance of the <see cref="MapRouteBuilder"/> class with the specified Kestrun host.
    /// </summary>
    /// <param name="server">The Kestrun host this route is associated with.</param>
    public MapRouteBuilder(KestrunHost server) => Server = server;
    /// <summary>
    /// The Kestrun host this route is associated with.
    /// Used by the MapRouteBuilder cmdlet.
    /// </summary>
    public KestrunHost Server { get; init; }

    /// <summary>
    /// Returns a string representation of the MapRouteBuilder, showing HTTP verbs and pattern.
    /// </summary>
    /// <returns>A string representation of the MapRouteBuilder.</returns>
    public override string ToString()
    {
        var verbs = HttpVerbs.Count > 0 ? string.Join(",", HttpVerbs) : "ANY";
        return $"{Server?.ApplicationName} {verbs} {Pattern}";
    }
}

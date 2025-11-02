/// <summary>
/// Attribute to specify runtime API context and notes for Kestrun routes or schedules.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KestrunRuntimeApiAttribute"/> class with the specified API contexts.
/// </remarks>
/// <param name="contexts">The API contexts in which the route or schedule can be executed.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class KestrunRuntimeApiAttribute(KestrunApiContext contexts) : KestrunAnnotation
{
    /// <summary>
    /// Gets the API contexts in which the route or schedule can be executed.
    /// </summary>
    public KestrunApiContext Contexts { get; } = contexts;

    /// <summary>
    /// Indicates whether the route is safe to be executed by untrusted callers.
    /// </summary>
    public bool SafeForUntrusted { get; set; }  // optional policy flag
    /// <summary>
    /// Optional notes or description for the route.
    /// </summary>
    public string? Notes { get; set; }
}

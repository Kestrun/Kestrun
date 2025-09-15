#pragma warning disable CA1050
/// <summary>
/// Specifies the API context in which a Kestrun route or schedule can be executed.
/// </summary>
[Flags]
public enum KestrunApiContext
{
    /// <summary>
    /// No API context specified.
    /// </summary>
    None = 0,
    /// <summary>
    /// Used during module/configuration time.
    /// </summary>
    Definition = 1 << 0, // module/configuration time
    /// <summary>
    /// Used inside HTTP route execution.
    /// </summary>
    Route = 1 << 1, // inside HTTP route execution

    /// <summary>
    /// Used during scheduled execution.
    /// </summary>
    Schedule = 1 << 2, // keep room for future split

    /// <summary>
    /// Used during both scheduled execution and module/configuration time (shorthand for Schedule | Definition).
    /// </summary>
    ScheduleAndDefinition = Schedule | Definition,
    /// <summary>
    /// Used during both HTTP route and scheduled execution (shorthand for Route | Schedule).
    /// </summary>
    Runtime = Route | Schedule,             // if you like a shorthand 
    /// <summary>
    /// Used in all available API contexts (Definition, Route, and Schedule).
    /// </summary>
    Everywhere = Definition | Route | Schedule
}
/// <summary>
/// Attribute to specify runtime API context and notes for Kestrun routes or schedules.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KestrunRuntimeApiAttribute"/> class with the specified API contexts.
/// </remarks>
/// <param name="contexts">The API contexts in which the route or schedule can be executed.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class KestrunRuntimeApiAttribute(KestrunApiContext contexts) : Attribute
{
    /// <summary>
    /// Gets the API contexts in which the route or schedule can be executed.
    /// </summary>
    public KestrunApiContext Contexts { get; } = contexts;

    /// <summary>
    /// Indicates whether the route is safe to be executed by untrusted callers.
    /// </summary>
    public bool SafeForUntrusted { get; init; } // optional policy flag
    /// <summary>
    /// Optional notes or description for the route.
    /// </summary>
    public string? Notes { get; init; }
}
#pragma warning restore CA1050


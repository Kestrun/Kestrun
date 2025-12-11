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

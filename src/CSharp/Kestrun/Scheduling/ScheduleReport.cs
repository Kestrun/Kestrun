namespace Kestrun.Scheduling;

/// <summary>
/// Represents a report of scheduled jobs at a specific time.
/// Contains the generation time and a list of job information.
/// This is useful for monitoring and auditing scheduled tasks.
/// </summary>
/// <param name="GeneratedAt">The time the report was generated.</param>
/// <param name="Jobs">The list of job information.</param>
/// <remarks>
/// This report can be used to track the status and execution history of scheduled jobs.
/// It is particularly useful for debugging and operational monitoring.
/// </remarks>  
public sealed record ScheduleReport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<JobInfo> Jobs);

using System.Collections.ObjectModel;
using System.Management.Automation;
using Kestrun.Logging.Enrichers.Extensions;

namespace Kestrun.Logging.Data;

/// <summary>
/// Wraps an ErrorRecord object to provide additional logging information.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ErrorRecordWrapper"/> class using the specified <see cref="ErrorRecord"/>.
/// </remarks>
/// <param name="errorRecord">The error record to wrap.</param>
public class ErrorRecordWrapper(ErrorRecord errorRecord)
{
    /// <summary>
    /// Gets the error category information associated with the error record.
    /// </summary>
    public ErrorCategoryInfo CategoryInfo { get; } = errorRecord.CategoryInfo;
    /// <summary>
    /// Gets or sets the error details associated with the error record.
    /// </summary>
    public ErrorDetails ErrorDetails { get; set; } = errorRecord.ErrorDetails;
    /// <summary>
    /// Gets the fully qualified error ID associated with the error record.
    /// </summary>
    public string FullyQualifiedErrorId { get; } = errorRecord.FullyQualifiedErrorId;
    /// <summary>
    /// Gets the invocation information wrapper associated with the error record.
    /// </summary>
    public InvocationInfoWrapper InvocationInfoWrapper { get; } = new InvocationInfoWrapper(errorRecord.InvocationInfo);
    /// <summary>
    /// Gets the pipeline iteration information associated with the error record.
    /// </summary>
    public ReadOnlyCollection<int> PipelineIterationInfo { get; } = errorRecord.PipelineIterationInfo;
    /// <summary>
    /// Gets the script stack trace associated with the error record.
    /// </summary>
    public string ScriptStackTrace { get; } = errorRecord.ScriptStackTrace;
    /// <summary>
    /// Gets the target object associated with the error record.
    /// </summary>
    public object TargetObject { get; } = errorRecord.TargetObject;
    /// <summary>
    /// Gets the exception message associated with the error record, if available.
    /// </summary>
    public string? ExceptionMessage { get; } = errorRecord.Exception?.Message;
    /// <summary>
    /// Gets the exception details associated with the error record, if available.
    /// </summary>
    public string? ExceptionDetails { get; } = errorRecord.Exception?.ToString();

    /// <summary>
    /// Returns a string representation of the current <see cref="ErrorRecordWrapper"/> instance.
    /// </summary>
    /// <returns>A string representation of the error record wrapper.</returns>
    public override string ToString() => this.ToTable();
}

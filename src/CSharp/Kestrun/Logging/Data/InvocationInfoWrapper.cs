using System.Management.Automation;
using System.Management.Automation.Language;
using Kestrun.Logging.Enrichers.Extensions;

namespace Kestrun.Logging.Data;

/// <summary>
/// Wraps the PowerShell InvocationInfo object and exposes its properties for logging purposes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InvocationInfoWrapper"/> class with the specified <see cref="InvocationInfo"/>.
/// </remarks>
/// <param name="invocationInfo">The PowerShell <see cref="InvocationInfo"/> object to wrap.</param>
public class InvocationInfoWrapper(InvocationInfo invocationInfo)
{
    /// <summary>
    /// Gets the dictionary of bound parameters for the PowerShell invocation.
    /// </summary>
    public Dictionary<string, object> BoundParameters { get; } = invocationInfo.BoundParameters;
    /// <summary>
    /// Gets the origin of the command (e.g., Runspace, Internal, etc.).
    /// </summary>
    public CommandOrigin CommandOrigin { get; } = invocationInfo.CommandOrigin;
    /// <summary>
    /// Gets the script extent that displays the position of the command in the script.
    /// </summary>
    public IScriptExtent DisplayScriptPosition { get; } = invocationInfo.DisplayScriptPosition;
    /// <summary>
    /// Gets a value indicating whether the command is expecting input.
    /// </summary>
    public bool ExpectingInput { get; } = invocationInfo.ExpectingInput;
    /// <summary>
    /// Gets the history ID of the PowerShell invocation.
    /// </summary>
    public long HistoryId { get; } = invocationInfo.HistoryId;
    /// <summary>
    /// Gets the name of the command being invoked.
    /// </summary>
    public string InvocationName { get; } = invocationInfo.InvocationName;
    /// <summary>
    /// Gets the line of the script where the command is invoked.
    /// </summary>
    public string Line { get; } = invocationInfo.Line;
    /// <summary>
    /// Gets the string representation of the command being invoked.
    /// </summary>
    public string? MyCommand { get; } = invocationInfo.MyCommand?.ToString();
    /// <summary>
    /// Gets the offset in the line where the command is invoked.
    /// </summary>
    public int OffsetInLine { get; } = invocationInfo.OffsetInLine;
    /// <summary>
    /// Gets the length of the pipeline for the PowerShell invocation.
    /// </summary>
    public int PipelineLength { get; } = invocationInfo.PipelineLength;
    /// <summary>
    /// Gets the position of the command in the pipeline for the PowerShell invocation.
    /// </summary>
    public int PipelinePosition { get; } = invocationInfo.PipelinePosition;
    /// <summary>
    /// Gets the position message for the PowerShell invocation.
    /// </summary>
    public string PositionMessage { get; } = invocationInfo.PositionMessage;
    /// <summary>
    /// Gets the path of the PowerShell command being executed.
    /// </summary>
    public string PSCommandPath { get; } = invocationInfo.PSCommandPath;
    /// <summary>
    /// Gets the script root path of the PowerShell script being executed.
    /// </summary>
    public string PSScriptRoot { get; } = invocationInfo.PSScriptRoot;
    /// <summary>
    /// Gets the line number in the script where the command is invoked.
    /// </summary>
    public int ScriptLineNumber { get; } = invocationInfo.ScriptLineNumber;
    /// <summary>
    /// Gets the name of the script where the command is invoked.
    /// </summary>
    public string ScriptName { get; } = invocationInfo.ScriptName;
    /// <summary>
    /// Gets the list of arguments that were not bound to parameters during the PowerShell invocation.
    /// </summary>
    public List<object> UnboundArguments { get; } = invocationInfo.UnboundArguments;

    /// <summary>
    /// Returns a string representation of the <see cref="InvocationInfoWrapper"/> object.
    /// </summary>
    /// <returns>A string representation of the current object.</returns>
    public override string ToString() => this.ToTable();
}

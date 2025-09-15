using Kestrun.Logging.Data;
using Kestrun.Logging.Utils.Console;
using Kestrun.Logging.Utils.Console.Extensions;


namespace Kestrun.Logging.Enrichers.Extensions;

/// <summary>
/// Provides extension methods for formatting error records and invocation info as tables.
/// </summary>
public static class ErrorRecordExtensions
{
    /// <summary>
    /// Formats the specified <see cref="ErrorRecordWrapper"/> as a table string.
    /// </summary>
    /// <param name="errorRecord">The error record to format.</param>
    /// <returns>A string representation of the error record as a table.</returns>
    public static string ToTable(this ErrorRecordWrapper errorRecord)
    {
        var table = new Table(new Padding(1));

        table.AddPropertyRow("CategoryInfo", errorRecord.CategoryInfo);
        table.AddPropertyRow("ErrorDetails", errorRecord.ErrorDetails);
        table.AddPropertyRow("FullyQualifiedErrorId", errorRecord.FullyQualifiedErrorId);
        table.AddPropertyRow("PipelineIterationInfo", string.Join(";", errorRecord.PipelineIterationInfo));
        table.AddPropertyRow("ScriptStackTrace", errorRecord.ScriptStackTrace);
        table.AddPropertyRow("TargetObject", errorRecord.TargetObject);

        return table.ToString();
    }

    /// <summary>
    /// Formats the specified <see cref="InvocationInfoWrapper"/> as a table string.
    /// </summary>
    /// <param name="invocationInfo">The invocation info to format.</param>
    /// <returns>A string representation of the invocation info as a table.</returns>
    public static string ToTable(this InvocationInfoWrapper invocationInfo)
    {
        var table = new Table(new Padding(1));

        var boundParams = string.Join(", ", invocationInfo.BoundParameters.Select(kv => $"{kv.Key}:{kv.Value}"));
        table.AddPropertyRow("BoundParameters", boundParams);
        table.AddPropertyRow("CommandOrigin", invocationInfo.CommandOrigin);
        table.AddPropertyRow("DisplayScriptPosition", invocationInfo.DisplayScriptPosition);
        table.AddPropertyRow("ExpectingInput", invocationInfo.ExpectingInput);
        table.AddPropertyRow("HistoryId", invocationInfo.HistoryId);
        table.AddPropertyRow("InvocationName", invocationInfo.InvocationName);
        table.AddPropertyRow("Line", invocationInfo.Line);
        table.AddPropertyRow("MyCommand", invocationInfo.MyCommand ?? string.Empty);
        table.AddPropertyRow("OffsetInLine", invocationInfo.OffsetInLine);
        table.AddPropertyRow("PipelineLength", invocationInfo.PipelineLength);
        table.AddPropertyRow("PipelinePosition", invocationInfo.PipelinePosition);
        table.AddPropertyRow("PSCommandPath", invocationInfo.PSCommandPath);
        table.AddPropertyRow("PSScriptRoot", invocationInfo.PSScriptRoot);
        table.AddPropertyRow("ScriptLineNumber", invocationInfo.ScriptLineNumber);
        table.AddPropertyRow("ScriptName", invocationInfo.ScriptName);
        table.AddPropertyRow("UnboundArguments", string.Join("; ", invocationInfo.UnboundArguments));

        return table.ToString();
    }
}

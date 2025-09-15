using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace Kestrun.Scripting;

/// <summary>
/// Exception thrown when C# script compilation fails.
/// Contains detailed diagnostic information about compilation errors.
/// </summary>
public class CompilationErrorException : Exception
{
    /// <summary>
    /// Gets the collection of diagnostics produced during compilation.
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationErrorException"/> class with a specified error message and diagnostics.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="diagnostics">The collection of diagnostics produced during compilation.</param>
    public CompilationErrorException(string message, ImmutableArray<Diagnostic> diagnostics)
        : base(FormatMessage(message, diagnostics)) => Diagnostics = diagnostics;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationErrorException"/> class with a specified error message, diagnostics, and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="diagnostics">The collection of diagnostics produced during compilation.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CompilationErrorException(string message, ImmutableArray<Diagnostic> diagnostics, Exception innerException)
        : base(FormatMessage(message, diagnostics), innerException) => Diagnostics = diagnostics;

    private static string FormatMessage(string baseMessage, ImmutableArray<Diagnostic> diagnostics)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine(baseMessage);
        _ = sb.AppendLine();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();

        if (errors.Any())
        {
            _ = sb.AppendLine($"Compilation Errors ({errors.Length}):");
            for (var i = 0; i < errors.Length; i++)
            {
                var error = errors[i];
                _ = sb.AppendLine($"  {i + 1}. {FormatDiagnostic(error)}");
            }
            _ = sb.AppendLine();
        }

        if (warnings.Any())
        {
            _ = sb.AppendLine($"Compilation Warnings ({warnings.Length}):");
            for (var i = 0; i < warnings.Length; i++)
            {
                var warning = warnings[i];
                _ = sb.AppendLine($"  {i + 1}. {FormatDiagnostic(warning)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var position = location.IsInSource ? $" at line {location.GetLineSpan().StartLinePosition.Line + 1}, column {location.GetLineSpan().StartLinePosition.Character + 1}" : "";

        return $"[{diagnostic.Id}] {diagnostic.GetMessage()}{position}";
    }

    /// <summary>
    /// Gets a formatted string containing all error details.
    /// </summary>
    public string GetDetailedErrorMessage() => Message;

    /// <summary>
    /// Gets only the error diagnostics (excluding warnings).
    /// </summary>
    public IEnumerable<Diagnostic> GetErrors() => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Gets only the warning diagnostics.
    /// </summary>
    public IEnumerable<Diagnostic> GetWarnings() => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
}

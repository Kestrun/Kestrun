using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Kestrun.Languages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serilog.Events;
using Kestrun.Models;



namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for validating C# scripts in the context of a KestrunHost.
/// </summary>
public static class KestrunHostScriptValidationExtensions
{
    /// <summary>
    /// Validates a C# script and returns compilation diagnostics without throwing exceptions.
    /// Useful for testing scripts before adding routes.
    /// </summary>
    /// <param name="host">The KestrunHost instance used for validation</param>
    /// <param name="code">The C# script code to validate</param>
    /// <param name="extraImports">Optional additional imports</param>
    /// <param name="extraRefs">Optional additional assembly references</param>
    /// <param name="languageVersion">C# language version to use</param>
    /// <returns>Compilation diagnostics including errors and warnings</returns>
    public static ImmutableArray<Diagnostic> ValidateCSharpScript(
        this KestrunHost host,
        string? code,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("ValidateCSharpScript() called: {@CodeLength}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        try
        {
            // Use the same script options as BuildCsDelegate
            var opts = ScriptOptions.Default
                       .WithImports("System", "System.Linq", "System.Threading.Tasks", "Microsoft.AspNetCore.Http")
                       .WithReferences(typeof(HttpContext).Assembly, typeof(KestrunResponse).Assembly)
                       .WithLanguageVersion(languageVersion);

            if (extraImports is { Length: > 0 })
            {
                opts = opts.WithImports(opts.Imports.Concat(extraImports));
            }

            if (extraRefs is { Length: > 0 })
            {
                opts = opts.WithReferences(opts.MetadataReferences
                                              .Concat(extraRefs.Select(r => MetadataReference.CreateFromFile(r.Location))));
            }

            var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
            return script.Compile();
        }
        catch (Exception ex)
        {
            // If there's an exception during script creation, create a synthetic diagnostic
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "KESTRUN001",
                    "Script validation error",
                    "Script validation failed: {0}",
                    "Compilation",
                    DiagnosticSeverity.Error,
                    true),
                Location.None,
                ex.Message);

            return [diagnostic];
        }
    }

    /// <summary>
    /// Checks if a C# script has compilation errors.
    /// </summary>
    /// <param name="host">The KestrunHost instance used for validation</param>
    /// <param name="code">The C# script code to check</param>
    /// <param name="extraImports">Optional additional imports</param>
    /// <param name="extraRefs">Optional additional assembly references</param>
    /// <param name="languageVersion">C# language version to use</param>
    /// <returns>True if the script compiles without errors, false otherwise</returns>
    public static bool IsCSharpScriptValid(
        this KestrunHost host,
        string code,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        var diagnostics = host.ValidateCSharpScript(code, extraImports, extraRefs, languageVersion);
        return !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Gets formatted error information for a C# script.
    /// </summary>
    /// <param name="host">The KestrunHost instance used for validation</param>
    /// <param name="code">The C# script code to check</param>
    /// <param name="extraImports">Optional additional imports</param>
    /// <param name="extraRefs">Optional additional assembly references</param>
    /// <param name="languageVersion">C# language version to use</param>
    /// <returns>Formatted error message, or null if no errors</returns>
    public static string? GetCSharpScriptErrors(this KestrunHost host,
        string code,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("GetCSharpScriptErrors() called: {@CodeLength}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        var diagnostics = host.ValidateCSharpScript(code, extraImports, extraRefs, languageVersion);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        if (errors.Length == 0)
        {
            return null;
        }

        try
        {
            // Create a temporary exception to format the errors
            var tempException = new Scripting.CompilationErrorException("Script validation errors:", diagnostics);
            return tempException.GetDetailedErrorMessage();
        }
        catch
        {
            // Fallback formatting if exception creation fails
            var sb = new StringBuilder();
            _ = sb.AppendLine($"Script has {errors.Length} compilation error(s):");
            for (var i = 0; i < errors.Length; i++)
            {
                _ = sb.AppendLine($"  {i + 1}. {errors[i].GetMessage()}");
            }
            return sb.ToString();
        }
    }
}

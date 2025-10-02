using System.Reflection;
using Kestrun.Languages;
using Kestrun.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.VisualBasic;
using KestrunCompilationErrorException = Kestrun.Scripting.CompilationErrorException;
using RoslynCompilationErrorException = Microsoft.CodeAnalysis.Scripting.CompilationErrorException;
using SerilogLogger = Serilog.ILogger;

namespace Kestrun.Health;

/// <summary>
/// Creates <see cref="IProbe"/> implementations backed by dynamic scripts.
/// </summary>
internal static class ScriptProbeFactory
{
    internal static IProbe Create(
        string name,
        IEnumerable<string>? tags,
        ScriptLanguage language,
        string code,
        SerilogLogger logger,
        Func<KestrunRunspacePoolManager>? runspaceAccessor,
        IReadOnlyDictionary<string, object?>? arguments,
        string[]? extraImports,
        Assembly[]? extraRefs)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return language switch
        {
            ScriptLanguage.PowerShell => CreatePowerShellProbe(name, tags, code, logger, runspaceAccessor, arguments),
            ScriptLanguage.CSharp => CreateCSharpProbe(name, tags, code, logger, arguments, extraImports, extraRefs),
            ScriptLanguage.VBNet => CreateVbProbe(name, tags, code, logger, arguments, extraImports, extraRefs),
            ScriptLanguage.Native => throw new NotSupportedException("Use AddProbe(Func<...>) for native probes."),
            ScriptLanguage.FSharp => throw new NotImplementedException("F# health probes are not yet supported."),
            ScriptLanguage.Python => throw new NotImplementedException("Python health probes are not yet supported."),
            ScriptLanguage.JavaScript => throw new NotImplementedException("JavaScript health probes are not yet supported."),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    /// <summary>
    /// Builds and returns a PowerShell script probe.
    /// </summary>
    /// <param name="name">The name of the probe.</param>
    /// <param name="tags">The tags associated with the probe.</param>
    /// <param name="code">The PowerShell code to execute.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="runspaceAccessor">Accessor for the PowerShell runspace pool manager.</param>
    /// <param name="arguments">Arguments to pass to the script.</param>
    /// <returns>A new <see cref="IProbe"/> instance representing the PowerShell script probe.</returns>
    private static IProbe CreatePowerShellProbe(
        string name,
        IEnumerable<string>? tags,
        string code,
        SerilogLogger logger,
        Func<KestrunRunspacePoolManager>? runspaceAccessor,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(runspaceAccessor);
        return new PowerShellScriptProbe(name, tags, code, logger, runspaceAccessor, arguments);
    }

    /// <summary>
    /// Builds and returns a C# script probe.
    /// </summary>
    /// <param name="name">The name of the probe.</param>
    /// <param name="tags">The tags associated with the probe.</param>
    /// <param name="code">The C# code to compile.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="arguments">The arguments for the script.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assemblies to reference.</param>
    /// <returns>A new <see cref="IProbe"/> instance representing the C# script probe.</returns>
    private static IProbe CreateCSharpProbe(
        string name,
        IEnumerable<string>? tags,
        string code,
        SerilogLogger logger,
        IReadOnlyDictionary<string, object?>? arguments,
        string[]? extraImports,
        Assembly[]? extraRefs)
    {
        try
        {
            var runner = BuildCSharpRunner(code, extraImports, extraRefs);
            return new CSharpScriptProbe(name, tags, runner, arguments, logger);
        }
        catch (RoslynCompilationErrorException ex)
        {
            logger.Error(ex, "Failed to compile C# health probe {Probe}.", name);
            throw;
        }
    }

    /// <summary>
    /// Builds and returns a VB.NET script probe.
    /// </summary>
    /// <param name="name">The name of the probe.</param>
    /// <param name="tags">The tags associated with the probe.</param>
    /// <param name="code">The VB.NET code to compile.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="arguments">The arguments for the script.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assemblies to reference.</param>
    /// <returns>A new <see cref="IProbe"/> instance representing the VB.NET script probe.</returns>
    /// <remarks>
    /// This method compiles the provided VB.NET code into a script runner that can be executed asynchronously.
    /// It allows for additional namespaces and assemblies to be included in the compilation context.
    /// </remarks>
    private static IProbe CreateVbProbe(
        string name,
        IEnumerable<string>? tags,
        string code,
        SerilogLogger logger,
        IReadOnlyDictionary<string, object?>? arguments,
        string[]? extraImports,
        Assembly[]? extraRefs)
    {
        try
        {
            var runner = VBNetDelegateBuilder.Compile<ProbeResult>(code, logger, extraImports, extraRefs, arguments, LanguageVersion.VisualBasic16_9);
            return new VbScriptProbe(name, tags, runner, arguments, logger);
        }
        catch (KestrunCompilationErrorException ex)
        {
            logger.Error(ex, "Failed to compile VB.NET health probe {Probe}.", name);
            throw;
        }
    }

    /// <summary>
    /// Builds a C# script runner for the given code, imports, and references.
    /// </summary>
    /// <param name="code">The C# code to compile.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assemblies to reference.</param>
    /// <returns>A script runner that can execute the compiled code.</returns>
    /// <remarks>
    /// This method compiles the provided C# code into a script runner that can be executed asynchronously.
    /// It allows for additional namespaces and assemblies to be included in the compilation context.
    /// </remarks>
    private static ScriptRunner<ProbeResult> BuildCSharpRunner(string code, string[]? extraImports, Assembly[]? extraRefs)
    {
        var options = ScriptOptions.Default
            .AddReferences(DelegateBuilder.BuildBaselineReferences())
            .AddReferences(typeof(ProbeResult).Assembly, typeof(ScriptProbeFactory).Assembly)
            .WithImports(DelegateBuilder.PlatformImports)
            .AddImports("Kestrun", "Kestrun.Health", "Kestrun.SharedState");

        if (extraImports is { Length: > 0 })
        {
            options = options.WithImports(options.Imports.Concat(extraImports).Distinct(StringComparer.Ordinal));
        }

        if (extraRefs is { Length: > 0 })
        {
            var additional = extraRefs
                .Where(static r => !string.IsNullOrEmpty(r.Location) && File.Exists(r.Location))
                .Select(static r => MetadataReference.CreateFromFile(r.Location));
            options = options.AddReferences(additional);
        }

        var script = CSharpScript.Create<ProbeResult>(code, options, typeof(CsGlobals));
        var diagnostics = script.Compile();
        return diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error)
            ? throw new RoslynCompilationErrorException("C# health probe compilation failed.", diagnostics)
            : script.CreateDelegate();
    }
}

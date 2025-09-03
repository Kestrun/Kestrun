using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serilog.Events;
using System.Security.Claims;
using Kestrun.Logging;

namespace Kestrun.Languages;


internal static class CSharpDelegateBuilder
{
    /// <summary>
    /// Builds a C# delegate for handling HTTP requests.
    /// </summary>
    /// <param name="code">The C# code to execute.</param>
    /// <param name="log">The logger instance.</param>
    /// <param name="args">Arguments to inject as variables into the script.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assemblies to reference.</param>
    /// <param name="languageVersion">The C# language version to use.</param>
    /// <returns>A delegate that handles HTTP requests.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the code is null or whitespace.</exception>
    /// <exception cref="CompilationErrorException">Thrown if the C# code compilation fails.</exception>
    /// <remarks>   
    /// This method compiles the provided C# code into a script and returns a delegate that can be used to handle HTTP requests.
    /// It supports additional imports and references, and can inject global variables into the script.
    /// The delegate will execute the provided C# code within the context of an HTTP request, allowing access to the request and response objects.
    /// </remarks>
    internal static RequestDelegate Build(
            string code, Serilog.ILogger log, Dictionary<string, object?>? args, string[]? extraImports,
            Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building C# delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code), "C# code cannot be null or whitespace.");
        }
        // 1. Compile the C# code into a script
        //    - Use CSharpScript.Create() to create a script with the provided code
        //    - Use ScriptOptions to specify imports, references, and language version
        //    - Inject the provided arguments into the globals
        var script = Compile(code, log, extraImports, extraRefs, null, languageVersion);

        // 2. Return a delegate that executes the script 
        //    - The delegate takes an HttpContext and returns a Task
        //    - It creates a KestrunContext and KestrunResponse from the HttpContext
        //    - It executes the script with the provided globals and locals
        //    - It applies the response to the HttpContext
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("C# delegate built successfully, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        return async ctx =>
        {
            try
            {
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.DebugSanitized("Preparing execution for C# script at {Path}", ctx.Request.Path);
                }

                var (Globals, Response, Context) = await DelegateBuilder.PrepareExecutionAsync(ctx, log, args).ConfigureAwait(false);

                // Execute the script with the current context and shared state
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.DebugSanitized("Executing C# script for {Path}", ctx.Request.Path);
                }

                _ = await script.RunAsync(Globals).ConfigureAwait(false);
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.DebugSanitized("C# script executed successfully for {Path}", ctx.Request.Path);
                }

                // Apply the response to the Kestrun context
                await DelegateBuilder.ApplyResponseAsync(ctx, Response, log).ConfigureAwait(false);
            }
            finally
            {
                await ctx.Response.CompleteAsync().ConfigureAwait(false);
            }
        };
    }

    /// <summary>
    /// Compiles the provided C# code into a script.
    /// This method supports additional imports and references, and can inject global variables into the script.
    /// It returns a compiled script that can be executed later.
    /// </summary>
    /// <param name="code">The C# code to compile.</param>
    /// <param name="log">The logger instance.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assembly references.</param>
    /// <param name="locals">Local variables to inject into the script.</param>
    /// <param name="languageVersion">The C# language version to use.</param>
    /// <returns>A compiled script that can be executed later.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the code is null or whitespace.</exception>
    /// <exception cref="CompilationErrorException">Thrown when there are compilation errors.</exception>
    /// <remarks>
    /// This method compiles the provided C# code into a script using Roslyn.
    /// It supports additional imports and references, and can inject global variables into the script.
    /// The script can be executed later with the provided globals and locals.
    /// It is useful for scenarios where dynamic C# code execution is required, such as in web applications or scripting environments.
    /// </remarks>
    internal static Script<object> Compile(
            string? code, Serilog.ILogger log, string[]? extraImports,
            Assembly[]? extraRefs, IReadOnlyDictionary<string, object?>? locals, LanguageVersion languageVersion = LanguageVersion.CSharp12
            )
    {
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Compiling C# script, length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code), "C# code cannot be null or whitespace.");
        }

        // References and imports
        var coreRefs = BuildBaselineReferences();
        var kestrunAssembly = typeof(Hosting.KestrunHost).Assembly; // Kestrun.dll
        var kestrunRef = MetadataReference.CreateFromFile(kestrunAssembly.Location);
        var kestrunNamespaces = CollectKestrunNamespaces(kestrunAssembly);

        var opts = CreateScriptOptions([], kestrunNamespaces, coreRefs, kestrunRef);
        opts = AddExtraImports(opts, extraImports);
        opts = AddExtraReferences(opts, extraRefs, log);

        // Optionally include all currently loaded assemblies to reduce missing reference issues.
        // This is a broader net and may include more assemblies than strictly necessary, but
        // Roslyn will de-duplicate by file path. This helps scenarios where user code touches
        // framework areas not pre-listed in core references (e.g., cryptography, diagnostics, etc.).
        var (loadedRefs, loadedCount) = CollectLoadedAssemblyReferences(log);
        if (loadedCount > 0)
        {
            // Avoid re-adding references already present
            var existingPaths = new HashSet<string>(opts.MetadataReferences
                .OfType<PortableExecutableReference>()
                .Select(r => r.FilePath ?? string.Empty)
                .Where(p => !string.IsNullOrEmpty(p)), StringComparer.OrdinalIgnoreCase);
            var newLoadedRefs = loadedRefs
                .Where(r => r is PortableExecutableReference pe && !string.IsNullOrEmpty(pe.FilePath) && !existingPaths.Contains(pe.FilePath))
                .ToArray();
            if (newLoadedRefs.Length > 0)
            {
                opts = opts.WithReferences(opts.MetadataReferences.Concat(newLoadedRefs));
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug("Added {RefCount} loaded assembly reference(s) (of {TotalLoaded}) for dynamic script compilation.", newLoadedRefs.Length, loadedCount);
                }
            }
        }

        // Globals/locals injection plus dynamic discovery of namespaces & assemblies needed
        var (CodeWithPreamble, DynamicImports, DynamicReferences) = BuildGlobalsAndLocalsPreamble(code, locals, log);
        code = CodeWithPreamble;

        if (DynamicImports.Count > 0)
        {
            var newImports = DynamicImports.Except(opts.Imports, StringComparer.Ordinal).ToArray();
            if (newImports.Length > 0)
            {
                opts = opts.WithImports(opts.Imports.Concat(newImports));
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug("Added {ImportCount} dynamic imports derived from globals/locals: {Imports}", newImports.Length, string.Join(", ", newImports));
                }
            }
        }

        if (DynamicReferences.Count > 0)
        {
            // Avoid duplicates by location
            var existingRefPaths = new HashSet<string>(opts.MetadataReferences
                .OfType<PortableExecutableReference>()
                .Select(r => r.FilePath ?? string.Empty)
                .Where(p => !string.IsNullOrEmpty(p)), StringComparer.OrdinalIgnoreCase);

            var newRefs = DynamicReferences
                .Where(r => !string.IsNullOrEmpty(r.Location) && File.Exists(r.Location) && !existingRefPaths.Contains(r.Location))
                .Select(r => MetadataReference.CreateFromFile(r.Location))
                .ToArray();

            if (newRefs.Length > 0)
            {
                opts = opts.WithReferences(opts.MetadataReferences.Concat(newRefs));
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug("Added {RefCount} dynamic assembly reference(s) derived from globals/locals.", newRefs.Length);
                }
            }
        }

        // Compile
        var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
        var diagnostics = CompileAndGetDiagnostics(script, log);
        ThrowIfDiagnosticsNull(diagnostics);
        ThrowOnErrors(diagnostics, log);
        LogWarnings(diagnostics, log);
        LogSuccessIfNoWarnings(diagnostics, log);

        return script;
    }

    /// <summary>Collects metadata references for all non-dynamic loaded assemblies with a physical location.</summary>
    /// <param name="log">Logger.</param>
    /// <returns>Tuple of references and total count considered.</returns>
    private static (IEnumerable<MetadataReference> Refs, int Total) CollectLoadedAssemblyReferences(Serilog.ILogger log)
    {
        try
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            var refs = new List<MetadataReference>(loaded.Length);
            var considered = 0;
            foreach (var a in loaded)
            {
                considered++;
                if (a.IsDynamic)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(a.Location) || !File.Exists(a.Location))
                {
                    continue;
                }
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(a.Location));
                }
                catch (Exception ex)
                {
                    if (log.IsEnabled(LogEventLevel.Debug))
                    {
                        log.Debug(ex, "Failed to add loaded assembly reference: {Assembly}", a.FullName);
                    }
                }
            }
            return (refs, considered);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to enumerate loaded assemblies for dynamic references.");
            return (Array.Empty<MetadataReference>(), 0);
        }
    }

    /// <summary>
    /// Builds the core assembly references for the script.
    /// </summary>
    /// <returns>The core assembly references.</returns>
    private static MetadataReference[] BuildBaselineReferences()
    {
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),            // System.Private.CoreLib
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),        // System.Linq
            MetadataReference.CreateFromFile(typeof(HttpContext).Assembly.Location),       // Microsoft.AspNetCore.Http
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),           // System.Console
            MetadataReference.CreateFromFile(typeof(StringBuilder).Assembly.Location),     // System.Text
            MetadataReference.CreateFromFile(typeof(Serilog.Log).Assembly.Location),       // Serilog
            MetadataReference.CreateFromFile(typeof(ClaimsPrincipal).Assembly.Location),   // System.Security.Claims
            MetadataReference.CreateFromFile(typeof(System.Security.Cryptography.X509Certificates.X509Certificate2).Assembly.Location) // System.Security.Cryptography.X509Certificates
        ];
    }

    /// <summary>
    /// Collects the namespaces from the Kestrun assembly.
    /// </summary>
    /// <param name="kestrunAssembly">The Kestrun assembly.</param>
    /// <returns>The collected namespaces.</returns>
    private static string[] CollectKestrunNamespaces(Assembly kestrunAssembly)
    {
        return [.. kestrunAssembly
            .GetExportedTypes()
            .Select(t => t.Namespace)
            .Where(ns => !string.IsNullOrEmpty(ns) && ns!.StartsWith("Kestrun", StringComparison.Ordinal))
            .Select(ns => ns!)
            .Distinct()];
    }

    /// <summary>
    /// Creates script options for the VB.NET script.
    /// </summary>
    /// <param name="platformImports">The platform-specific namespaces to import.</param>
    /// <param name="kestrunNamespaces">The Kestrun-specific namespaces to import.</param>
    /// <param name="coreRefs">The core assembly references to include.</param>
    /// <param name="kestrunRef">The Kestrun assembly reference to include.</param>
    /// <returns>The created script options.</returns>
    private static ScriptOptions CreateScriptOptions(
        IEnumerable<string> platformImports,
        IEnumerable<string> kestrunNamespaces,
        IEnumerable<MetadataReference> coreRefs,
        MetadataReference kestrunRef)
    {
        var allImports = platformImports.Concat(kestrunNamespaces) ?? [];
        // Keep default references then add our core + Kestrun to avoid losing essential BCL assemblies
        var opts = ScriptOptions.Default
            .WithImports(allImports)
            .AddReferences(coreRefs)
            .AddReferences(kestrunRef);
        return opts;
    }

    /// <summary>
    /// Adds extra using directives to the script options.
    /// </summary>
    /// <param name="opts">The script options to modify.</param>
    /// <param name="extraImports">The extra using directives to add.</param>
    /// <returns>The modified script options.</returns>
    private static ScriptOptions AddExtraImports(ScriptOptions opts, string[]? extraImports)
    {
        extraImports ??= ["Kestrun"];
        if (!extraImports.Contains("Kestrun"))
        {
            var importsList = extraImports.ToList();
            importsList.Add("Kestrun");
            extraImports = [.. importsList];
        }
        return extraImports.Length > 0
            ? opts.WithImports(opts.Imports.Concat(extraImports))
            : opts;
    }

    /// <summary>
    /// Adds extra assembly references to the script options.
    /// </summary>
    /// <param name="opts">The script options to modify.</param>
    /// <param name="extraRefs">The extra assembly references to add.</param>
    /// <param name="log">The logger to use for logging.</param>
    /// <returns>The modified script options.</returns>
    private static ScriptOptions AddExtraReferences(ScriptOptions opts, Assembly[]? extraRefs, Serilog.ILogger log)
    {
        if (extraRefs is not { Length: > 0 })
        {
            return opts;
        }

        foreach (var r in extraRefs)
        {
            if (string.IsNullOrEmpty(r.Location))
            {
                log.Warning("Skipping dynamic assembly with no location: {Assembly}", r.FullName);
            }
            else if (!File.Exists(r.Location))
            {
                log.Warning("Skipping missing assembly file: {Location}", r.Location);
            }
        }

        var safeRefs = extraRefs
            .Where(r => !string.IsNullOrEmpty(r.Location) && File.Exists(r.Location))
            .Select(r => MetadataReference.CreateFromFile(r.Location));

        return opts.WithReferences(opts.MetadataReferences.Concat(safeRefs));
    }

    /// <summary>
    /// Prepends global and local variable declarations to the provided code.
    /// </summary>
    /// <param name="code">The original code to modify.</param>
    /// <param name="locals">The local variables to include.</param>
    /// <returns>The modified code with global and local variable declarations.</returns>
    /// <summary>Builds the preamble variable declarations for globals &amp; locals and discovers required namespaces and assemblies.</summary>
    /// <param name="log">Logger instance.</param>
    /// <returns>Tuple containing code with preamble, dynamic imports, dynamic references.</returns>
    private static (string CodeWithPreamble, List<string> DynamicImports, List<Assembly> DynamicReferences) BuildGlobalsAndLocalsPreamble(
        string? code,
        IReadOnlyDictionary<string, object?>? locals,
        Serilog.ILogger log)
    {
        var preambleBuilder = new StringBuilder();
        var allGlobals = SharedStateStore.Snapshot();
        var merged = new Dictionary<string, (string Dict, object? Value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in allGlobals)
        {
            merged[g.Key] = ("Globals", g.Value);
        }
        if (locals is { Count: > 0 })
        {
            foreach (var l in locals)
            {
                merged[l.Key] = ("Locals", l.Value);
            }
        }

        var dynamicImports = new HashSet<string>(StringComparer.Ordinal);
        var dynamicRefs = new HashSet<Assembly>();

        foreach (var kvp in merged)
        {
            var valueType = kvp.Value.Value?.GetType();
            var typeName = FormatTypeName(valueType);
            _ = preambleBuilder.AppendLine($"var {kvp.Key} = ({typeName}){kvp.Value.Dict}[\"{kvp.Key}\"]; ");

            if (valueType != null)
            {
                if (!string.IsNullOrEmpty(valueType.Namespace))
                {
                    _ = dynamicImports.Add(valueType.Namespace!); // capture added namespace
                }
                // Include generic argument namespaces as well
                if (valueType.IsGenericType)
                {
                    foreach (var ga in valueType.GetGenericArguments())
                    {
                        if (!string.IsNullOrEmpty(ga.Namespace))
                        {
                            _ = dynamicImports.Add(ga.Namespace!); // capture generic arg namespace
                        }
                        _ = dynamicRefs.Add(ga.Assembly); // capture generic arg assembly
                    }
                }
                _ = dynamicRefs.Add(valueType.Assembly); // capture value type assembly
            }
        }

        if (log.IsEnabled(LogEventLevel.Debug) && (dynamicImports.Count > 0 || dynamicRefs.Count > 0))
        {
            log.Debug("Discovered {ImportCount} dynamic import(s) and {RefCount} reference(s) from globals/locals.", dynamicImports.Count, dynamicRefs.Count);
        }

        return (
            preambleBuilder.Length > 0 ? preambleBuilder + (code ?? string.Empty) : code ?? string.Empty,
            dynamicImports.ToList(),
            dynamicRefs.Where(r => !string.IsNullOrEmpty(r.Location)).ToList()
        );
    }

    // Produces a C# friendly type name for reflection types (handles generics, arrays, nullable, and fallbacks).
    private static string FormatTypeName(Type? t)
    {
        if (t == null)
        {
            return "object";
        }
        if (t.IsGenericParameter)
        {
            return "object";
        }
        if (t.IsArray)
        {
            return FormatTypeName(t.GetElementType()) + "[]";
        }
        // Nullable<T>
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return FormatTypeName(t.GetGenericArguments()[0]) + "?";
        }
        if (t.IsGenericType)
        {
            try
            {
                var genericDefName = t.Name;
                var tickIndex = genericDefName.IndexOf('`');
                if (tickIndex > 0)
                {
                    genericDefName = genericDefName[..tickIndex];
                }
                var args = t.GetGenericArguments().Select(FormatTypeName);
                return (t.Namespace != null ? t.Namespace + "." : string.Empty) + genericDefName + "<" + string.Join(",", args) + ">";
            }
            catch
            {
                return "object";
            }
        }
        // Non generic
        return t.FullName ?? t.Name ?? "object";
    }

    /// <summary>
    /// Compiles the provided VB.NET script and returns any diagnostics.
    /// </summary>
    /// <param name="script">The VB.NET script to compile.</param>
    /// <param name="log">The logger to use for logging.</param>
    /// <returns>A collection of diagnostics produced during compilation, or null if compilation failed.</returns>
    private static ImmutableArray<Diagnostic>? CompileAndGetDiagnostics(Script<object> script, Serilog.ILogger log)
    {
        try
        {
            return script.Compile();
        }
        catch (CompilationErrorException ex)
        {
            log.Error(ex, "C# script compilation failed with errors.");
            return null;
        }
    }

    private static void ThrowIfDiagnosticsNull(ImmutableArray<Diagnostic>? diagnostics)
    {
        if (diagnostics == null)
        {
            throw new CompilationErrorException("C# script compilation failed with no diagnostics.", []);
        }
    }

    /// <summary>
    /// Throws a CompilationErrorException if the diagnostics are null.
    /// </summary>
    /// <param name="diagnostics">The compilation diagnostics.</param>
    /// <param name="log">The logger to use for logging.</param>
    /// <exception cref="CompilationErrorException"></exception>
    private static void ThrowOnErrors(ImmutableArray<Diagnostic>? diagnostics, Serilog.ILogger log)
    {
        if (diagnostics?.Any(d => d.Severity == DiagnosticSeverity.Error) != true)
        {
            return;
        }

        var errors = diagnostics?.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors is not { Length: > 0 })
        {
            return;
        }

        var sb = new StringBuilder();
        _ = sb.AppendLine($"C# script compilation completed with {errors.Length} error(s):");
        foreach (var error in errors)
        {
            var location = error.Location.IsInSource
                ? $" at line {error.Location.GetLineSpan().StartLinePosition.Line + 1}"
                : string.Empty;
            var msg = $"  Error [{error.Id}]: {error.GetMessage()}{location}";
            log.Error(msg);
            _ = sb.AppendLine(msg);
        }
        throw new CompilationErrorException("C# route code compilation failed\n" + sb.ToString(), diagnostics ?? []);
    }

    /// <summary>
    /// Logs warning messages if the compilation succeeded with warnings.
    /// </summary>
    /// <param name="diagnostics">The compilation diagnostics.</param>
    /// <param name="log">The logger to use for logging.</param>
    private static void LogWarnings(ImmutableArray<Diagnostic>? diagnostics, Serilog.ILogger log)
    {
        var warnings = diagnostics?.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings is not null && warnings.Length != 0)
        {
            log.Warning($"C# script compilation completed with {warnings.Length} warning(s):");
            foreach (var warning in warnings)
            {
                var location = warning.Location.IsInSource
                    ? $" at line {warning.Location.GetLineSpan().StartLinePosition.Line + 1}"
                    : string.Empty;
                log.Warning($"  Warning [{warning.Id}]: {warning.GetMessage()}{location}");
            }
        }
    }

    /// <summary>
    /// Logs a success message if the compilation succeeded without warnings.
    /// </summary>
    /// <param name="diagnostics">The compilation diagnostics.</param>
    /// <param name="log">The logger to use for logging.</param>
    private static void LogSuccessIfNoWarnings(ImmutableArray<Diagnostic>? diagnostics, Serilog.ILogger log)
    {
        var warnings = diagnostics?.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings != null && warnings.Length == 0 && log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("C# script compiled successfully with no warnings.");
        }
    }
}
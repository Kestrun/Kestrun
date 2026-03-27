using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.VisualBasic;
using Serilog.Events;
using Microsoft.CodeAnalysis;
using Kestrun.Utilities;
using System.Security.Claims;
using Kestrun.Logging;
using Kestrun.Hosting;

namespace Kestrun.Languages;

internal static class VBNetDelegateBuilder
{
    /// <summary>
    /// The marker that indicates where user code starts in the VB.NET script.
    /// This is used to ensure that the user code is correctly placed within the generated module.
    /// </summary>
    private const string StartMarker = "' ---- User code starts here ----";

    /// <summary>
    /// Builds a VB.NET delegate for Kestrun routes.
    /// </summary>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="code">The VB.NET code to compile.</param>
    /// <param name="args">The arguments to pass to the script.</param>
    /// <param name="extraImports">Optional additional namespaces to import in the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference in the script.</param>
    /// <param name="languageVersion">The VB.NET language version to use for compilation.</param>
    /// <returns>A delegate that takes CsGlobals and returns a Task.</returns>
    /// <exception cref="CompilationErrorException">Thrown if the compilation fails with errors.</exception>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    internal static RequestDelegate Build(KestrunHost host,
        string code, Dictionary<string, object?>? args, string[]? extraImports,
        Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.VisualBasic16_9)
    {
        var log = host.Logger;
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building VB.NET delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
               code.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code), "VB.NET code cannot be null or whitespace.");
        }
        // 1. Compile the VB.NET code into a script
        //    - Use VisualBasicScript.Create() to create a script with the provided code
        //    - Use ScriptOptions to specify imports, references, and language version
        //    - Inject the provided arguments into the globals
        var script = Compile<bool>(host: host, code: code, extraImports: extraImports, extraRefs: extraRefs, null, languageVersion);

        // 2. Build the per-request delegate
        //    - This delegate will be executed for each request
        //    - It will create a KestrunContext and CsGlobals, then execute the script with these globals
        //    - The script can access the request context and shared state store
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
                    log.Debug("Preparing execution for C# script at {Path}", ctx.Request.Path);
                }

                var (Globals, Response, Context) = await DelegateBuilder.PrepareExecutionAsync(host, ctx, args).ConfigureAwait(false);

                // Execute the script with the current context and shared state
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.DebugSanitized("Executing VB.NET script for {Path}", ctx.Request.Path);
                }

                _ = await script(Globals).ConfigureAwait(false);
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.DebugSanitized("VB.NET script executed successfully for {Path}", ctx.Request.Path);
                }

                // Apply the response to the Kestrun context
                await DelegateBuilder.ApplyResponseAsync(ctx, Response, log).ConfigureAwait(false);
            }
            finally
            {
                // Do not complete the response here; allow downstream middleware (e.g., StatusCodePages)
                // to produce a body for status-only responses when needed.
            }
        };
    }

    /// <summary>
    /// Decide the VB return type string that matches TResult
    /// </summary>
    /// <param name="t">The type to get the VB return type for.</param>
    /// <returns> The VB.NET return type as a string.</returns>
    private static string GetVbReturnType(Type t)
    {
        if (t == typeof(bool))
        {
            return "Boolean";
        }

        if (t == typeof(IEnumerable<Claim>))
        {
            return "System.Collections.Generic.IEnumerable(Of System.Security.Claims.Claim)";
        }

        // Fallback so it still compiles even for object / string / etc.
        return "Object";
    }

    /// <summary>
    /// Compiles the provided VB.NET code into a delegate that can be executed with CsGlobals.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the delegate.</typeparam>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="code">The VB.NET code to compile.</param>
    /// <param name="extraImports">Optional additional namespaces to import in the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference in the script.</param>
    /// <param name="locals">Optional local variables to provide to the script.</param>
    /// <param name="languageVersion">The VB.NET language version to use for compilation.</param>
    /// <returns>A delegate that takes CsGlobals and returns a Task.</returns>
    /// <exception cref="CompilationErrorException">Thrown if the compilation fails with errors.</exception>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    internal static Func<CsGlobals, Task<TResult>> Compile<TResult>(
        KestrunHost host,
            string? code, string[]? extraImports,
            Assembly[]? extraRefs, IReadOnlyDictionary<string, object?>? locals, LanguageVersion languageVersion
        )
    {
        var log = host.Logger;
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building VB.NET delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
               code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code), "VB.NET code cannot be null or whitespace.");
        }

        extraImports ??= [];
        extraImports = [.. extraImports, "System.Collections.Generic", "System.Linq", "System.Security.Claims"];

        var (dynamicImports, dynamicRefs) = CollectDynamicMetadata(host, locals);
        if (dynamicImports.Count > 0)
        {
            var mergedImports = extraImports.Concat(dynamicImports)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (log.IsEnabled(LogEventLevel.Debug) && mergedImports.Length != extraImports.Length)
            {
                log.Debug("Added {Count} dynamic VB imports from globals/locals.", mergedImports.Length - extraImports.Length);
            }
            extraImports = mergedImports;
        }

        // 🔧 1.  Build a real VB file around the user snippet
        var source = BuildWrappedSource(code, extraImports, vbReturnType: GetVbReturnType(typeof(TResult)),
            locals: locals);

        // Prepares the source code for compilation.
        var startLine = GetStartLineOrThrow(source, log);

        // Parse the source code into a syntax tree
        // This will allow us to analyze and compile the code
        var tree = VisualBasicSyntaxTree.ParseText(
                   source,
                   new VisualBasicParseOptions(languageVersion));

        var refs = BuildMetadataReferences(extraRefs, extraImports, dynamicRefs);
        // 🔧 3.  Normal DLL compilation
        var compilation = VisualBasicCompilation.Create(
                 assemblyName: $"RouteScript_{Guid.NewGuid():N}",
                 syntaxTrees: [tree],
                 references: refs,
                 options: new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms) ?? throw new InvalidOperationException("Failed to compile VB.NET script.");
        // 🔧 4.  Log the compilation result
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("VB.NET script compilation completed, assembly size={Size} bytes", ms.Length);
        }

        // 🔧 5. Handle diagnostics
        ThrowIfErrors(emitResult.Diagnostics, startLine, log);
        // Log any warnings from the compilation process
        LogWarnings(emitResult.Diagnostics, startLine, log);

        // If there are no errors, log a debug message
        if (emitResult.Success && log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("VB.NET script compiled successfully with no errors.");
        }

        // If there are no errors, proceed to load the assembly and create the delegate
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("VB.NET script compiled successfully, loading assembly...");
        }

        ms.Position = 0;
        return LoadDelegateFromAssembly<TResult>(ms.ToArray());
    }

    /// <summary>
    /// Prepares the source code for compilation.
    /// </summary>
    /// <param name="source">The source code to prepare.</param>
    /// <param name="log">The logger instance.</param>
    /// <returns>The prepared source code.</returns>
    /// <exception cref="ArgumentException">Thrown when the source code is invalid.</exception>
    private static int GetStartLineOrThrow(string source, Serilog.ILogger log)
    {
        var startIndex = source.IndexOf(StartMarker, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            throw new ArgumentException($"VB.NET code must contain the marker '{StartMarker}' to indicate where user code starts.");
        }

        var startLine = CcUtilities.GetLineNumber(source, startIndex);
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("VB.NET script starts at line {LineNumber}", startLine);
        }

        return startLine;
    }

    /// <summary>
    /// Prepares the metadata references for the VB.NET script.
    /// </summary>
    /// <param name="extraRefs">The extra references to include.</param>
    /// <param name="imports">The effective imports used by the generated source.</param>
    /// <param name="dynamicRefs">Assemblies inferred from host globals and compile-time locals.</param>
    /// <returns>An enumerable of metadata references.</returns>
    private static IEnumerable<MetadataReference> BuildMetadataReferences(
        Assembly[]? extraRefs,
        IEnumerable<string> imports,
        IEnumerable<Assembly> dynamicRefs)
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMetadataReferenceLocations(locations, DelegateBuilder.BuildBaselineReferences());
        AddAssemblyLocation(locations, typeof(Microsoft.VisualBasic.Constants).Assembly);
        AddAssemblyLocation(locations, typeof(CsGlobals).Assembly);
        AddLoadedAssemblyLocation(locations, "System.Runtime");
        AddLoadedAssemblyLocation(locations, "netstandard");
        AddImportAssemblyLocations(locations, imports);
        AddAssemblyLocations(locations, dynamicRefs);
        AddAssemblyLocations(locations, extraRefs);

        return locations.Select(static location => MetadataReference.CreateFromFile(location));
    }

    private static bool IsSatelliteAssembly(Assembly a)
    {
        try
        {
            var name = a.GetName();
            if (name.Name != null && name.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var loc = a.Location;
            if (!string.IsNullOrEmpty(loc) && loc.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
            // If we can't inspect it, be conservative and treat as non-satellite
        }
        return false;
    }

    /// <summary>
    /// Collects dynamic imports and assembly references from the types of the provided locals and shared globals.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="locals">The local variables to inspect.</param>
    /// <returns>A set of unique namespace strings and the corresponding assembly references.</returns>
    private static (HashSet<string> Imports, HashSet<Assembly> References) CollectDynamicMetadata(
        KestrunHost host,
        IReadOnlyDictionary<string, object?>? locals)
    {
        var imports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new HashSet<Assembly>();
        foreach (var g in host.SharedState.Snapshot())
        {
            AddTypeMetadata(g.Value?.GetType(), imports, references);
        }

        if (locals is { Count: > 0 })
        {
            foreach (var l in locals)
            {
                AddTypeMetadata(l.Value?.GetType(), imports, references);
            }
        }

        return (imports, references);
    }

    /// <summary>
    /// Adds the namespace and assembly metadata for the supplied type, including generic arguments and array elements.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="imports">The namespace set to update.</param>
    /// <param name="references">The assembly set to update.</param>
    private static void AddTypeMetadata(Type? type, HashSet<string> imports, HashSet<Assembly> references)
    {
        if (type == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(type.Namespace))
        {
            _ = imports.Add(type.Namespace);
        }

        _ = references.Add(type.Assembly);

        if (type.IsGenericType)
        {
            foreach (var genericArgument in type.GetGenericArguments())
            {
                AddTypeMetadata(genericArgument, imports, references);
            }
        }

        if (type.IsArray)
        {
            AddTypeMetadata(type.GetElementType(), imports, references);
        }
    }

    /// <summary>
    /// Adds assembly locations needed for the supplied import namespaces.
    /// </summary>
    /// <param name="locations">The reference location set to update.</param>
    /// <param name="imports">The imports used by the generated source.</param>
    private static void AddImportAssemblyLocations(HashSet<string> locations, IEnumerable<string> imports)
    {
        var importSet = imports
            .Where(importName => !string.IsNullOrWhiteSpace(importName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (importSet.Count == 0)
        {
            return;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(IsReferenceableAssembly))
        {
            if (importSet.Any(importName => NamespaceExistsInAssembly(assembly, importName)))
            {
                AddAssemblyLocation(locations, assembly);
            }
        }
    }

    /// <summary>
    /// Adds all safe assembly locations from the provided collection.
    /// </summary>
    /// <param name="locations">The reference location set to update.</param>
    /// <param name="assemblies">The assemblies to inspect.</param>
    private static void AddAssemblyLocations(HashSet<string> locations, IEnumerable<Assembly>? assemblies)
    {
        if (assemblies == null)
        {
            return;
        }

        foreach (var assembly in assemblies)
        {
            AddAssemblyLocation(locations, assembly);
        }
    }

    /// <summary>
    /// Adds file paths from existing metadata references to the location set.
    /// </summary>
    /// <param name="locations">The reference location set to update.</param>
    /// <param name="references">The metadata references to inspect.</param>
    private static void AddMetadataReferenceLocations(HashSet<string> locations, IEnumerable<MetadataReference> references)
    {
        foreach (var reference in references.OfType<PortableExecutableReference>())
        {
            var filePath = reference.FilePath;
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                _ = locations.Add(filePath);
            }
        }
    }

    /// <summary>
    /// Adds a loaded assembly by simple name when it is available and safe to reference.
    /// </summary>
    /// <param name="locations">The reference location set to update.</param>
    /// <param name="assemblyName">The simple assembly name to resolve.</param>
    private static void AddLoadedAssemblyLocation(HashSet<string> locations, string assemblyName)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
        if (assembly != null)
        {
            AddAssemblyLocation(locations, assembly);
        }
    }

    /// <summary>
    /// Adds an assembly location when the assembly can be safely referenced by Roslyn.
    /// </summary>
    /// <param name="locations">The reference location set to update.</param>
    /// <param name="assembly">The assembly to inspect.</param>
    private static void AddAssemblyLocation(HashSet<string> locations, Assembly assembly)
    {
        if (!IsReferenceableAssembly(assembly))
        {
            return;
        }

        try
        {
            _ = locations.Add(assembly.Location);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Determines whether the assembly can be safely used as a metadata reference.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns><see langword="true"/> when the assembly has a stable file location and is not a satellite assembly.</returns>
    private static bool IsReferenceableAssembly(Assembly assembly)
        => !assembly.IsDynamic && SafeHasLocation(assembly) && !IsSatelliteAssembly(assembly);

    /// <summary>
    /// Determines whether the supplied namespace exists in the assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <param name="namespaceName">The namespace to search for.</param>
    /// <returns><see langword="true"/> when the namespace or one of its children is present.</returns>
    private static bool NamespaceExistsInAssembly(Assembly assembly, string namespaceName)
    {
        try
        {
            foreach (var type in assembly.DefinedTypes)
            {
                var currentNamespace = type.Namespace;
                if (currentNamespace == null)
                {
                    continue;
                }

                if (currentNamespace.Equals(namespaceName, StringComparison.OrdinalIgnoreCase)
                    || currentNamespace.StartsWith(namespaceName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // If we can't load all types, be conservative and assume the namespace is not present
        }

        return false;
    }
    private static bool SafeHasLocation(Assembly a)
    {
        try
        {
            var loc = a.Location; // may throw for some dynamic contexts
            return !string.IsNullOrEmpty(loc) && File.Exists(loc);
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Logs any warnings from the compilation process.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to check.</param>
    /// <param name="startLine">The starting line number.</param>
    /// <param name="log">The logger instance.</param>
    private static void LogWarnings(ImmutableArray<Diagnostic> diagnostics, int startLine, Serilog.ILogger log)
    {
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        // If there are no warnings, log a debug message
        if (warnings.Length == 0)
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("VB.NET script compiled successfully with no warnings.");
            }

            return;
        }

        log.Warning($"VBNet script compilation completed with {warnings.Length} warning(s):");
        foreach (var warning in warnings)
        {
            var location = warning.Location.IsInSource
                ? $" at line {warning.Location.GetLineSpan().StartLinePosition.Line - startLine + 1}"
                : "";
            log.Warning($"  Warning [{warning.Id}]: {warning.GetMessage()}{location}");
        }
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("VB.NET script compiled with warnings: {Count}", warnings.Length);
        }
    }

    /// <summary>
    /// Throws an exception if there are compilation errors.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to check.</param>
    /// <param name="startLine">The starting line number.</param>
    /// <param name="log">The logger instance.</param>
    private static void ThrowIfErrors(ImmutableArray<Diagnostic> diagnostics, int startLine, Serilog.ILogger log)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length == 0)
        {
            return;
        }

        log.Error($"VBNet script compilation completed with {errors.Length} error(s):");
        var sb = new StringBuilder();
        _ = sb.AppendLine("VBNet route code compilation failed:");
        foreach (var error in errors)
        {
            var location = error.Location.IsInSource
                ? $" at line {error.Location.GetLineSpan().StartLinePosition.Line - startLine + 1}"
                : "";
            var msg = $"  Error [{error.Id}]: {error.GetMessage()}{location}";
            log.Error(msg);
            _ = sb.AppendLine(msg);
        }
        throw new CompilationErrorException(sb.ToString().TrimEnd(), diagnostics);
    }

    /// <summary>
    /// Loads a delegate from the provided assembly bytes.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="asmBytes">The assembly bytes.</param>
    /// <returns>A delegate that can be invoked with the specified globals.</returns>
    private static Func<CsGlobals, Task<TResult>> LoadDelegateFromAssembly<TResult>(byte[] asmBytes)
    {
        var asm = Assembly.Load(asmBytes);
        var runMethod = asm.GetType("RouteScript")!
                           .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var delegateType = typeof(Func<,>).MakeGenericType(
            typeof(CsGlobals),
            typeof(Task<>).MakeGenericType(typeof(TResult)));

        return (Func<CsGlobals, Task<TResult>>)runMethod.CreateDelegate(delegateType);
    }

    /// <summary>
    /// Builds the wrapped source code for the VB.NET script.
    /// </summary>
    /// <param name="code">The user-provided code to wrap.</param>
    /// <param name="extraImports">Additional imports to include.</param>
    /// <param name="vbReturnType">The return type of the VB.NET function.</param>
    /// <param name="locals">Local variables to bind to the script.</param>
    /// <returns>The wrapped source code.</returns>
    private static string BuildWrappedSource(string? code, IEnumerable<string>? extraImports,
    string vbReturnType, IReadOnlyDictionary<string, object?>? locals = null
       )
    {
        var sb = new StringBuilder();

        // common + caller-supplied Imports
        var builtIns = new[] {
        "System", "System.Threading.Tasks",
        "Kestrun", "Kestrun.Models",
          "Microsoft.VisualBasic",
          "Kestrun.Languages"
        };

        foreach (var ns in builtIns.Concat(extraImports ?? [])
                                   .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _ = sb.AppendLine($"Imports {ns}");
        }

        _ = sb.AppendLine($"""
                Public Module RouteScript
                    Public Async Function Run(g As CsGlobals) As Task(Of {vbReturnType})
                        Await Task.Yield() ' placeholder await
                        Dim Request  = g.Context?.Request
                        Dim Response = g.Context?.Response
                        Dim Context  = g.Context
        """);

        // only emit these _when_ you called Compile with locals:
        if (locals?.ContainsKey("username") ?? false)
        {
            _ = sb.AppendLine("""
        ' only bind creds if someone passed them in
                        Dim username As String = CStr(g.Locals("username"))
        """);
        }

        if (locals?.ContainsKey("password") ?? false)
        {
            _ = sb.AppendLine("""
                        Dim password As String = CStr(g.Locals("password"))
        """);
        }

        if (locals?.ContainsKey("providedKey") == true)
        {
            _ = sb.AppendLine("""
        ' only bind keys if someone passed them in
                        Dim providedKey As String = CStr(g.Locals("providedKey"))
        """);
        }

        if (locals?.ContainsKey("providedKeyBytes") == true)
        {
            _ = sb.AppendLine("""
                        Dim providedKeyBytes As Byte() = CType(g.Locals("providedKeyBytes"), Byte())
        """);
        }

        if (locals?.ContainsKey("identity") == true)
        {
            _ = sb.AppendLine("""
                        Dim identity As String = CStr(g.Locals("identity"))
        """);
        }

        // add the Marker for user code
        _ = sb.AppendLine(StartMarker);
        // ---- User code starts here ----

        if (!string.IsNullOrEmpty(code))
        {
            // indent the user snippet so VB is happy
            _ = sb.AppendLine(string.Join(
                Environment.NewLine,
                code.Split('\n').Select(l => "        " + l.TrimEnd('\r'))));
        }
        _ = sb.AppendLine("""

                End Function
            End Module
    """);
        return sb.ToString();
    }
}

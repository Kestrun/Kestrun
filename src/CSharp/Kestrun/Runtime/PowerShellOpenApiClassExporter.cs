using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Kestrun.Runtime;

/// <summary>
/// Exports OpenAPI component classes into a compiled assembly so types have stable identity across runspaces.
/// </summary>
public static class PowerShellOpenApiClassExporter
{
    /// <summary>
    /// Holds valid class names to be used as type in the OpenAPI function definitions.
    /// </summary>
    public static List<string> ValidClassNames { get; } = [];

#if NET9_0_OR_GREATER
    private static readonly Lock ValidClassNamesLock = new();
#else
    private static readonly object ValidClassNamesLock = new();
#endif

    /// <summary>
    /// Thread-safe lookup for <see cref="ValidClassNames"/>.
    /// </summary>
    public static bool IsValidClassName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (ValidClassNamesLock)
        {
            // Stored values are case-sensitive C# identifiers.
            for (var i = 0; i < ValidClassNames.Count; i++)
            {
                if (string.Equals(ValidClassNames[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Exports OpenAPI component classes found in loaded assemblies
    /// into a compiled assembly.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The path to the compiled assembly containing the class definitions.</returns>
    public static string ExportOpenApiClasses(Serilog.ILogger logger)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(ShouldScanAssemblyForComponents)
            .ToArray();
        return ExportOpenApiClasses(assemblies, logger);
    }

    /// <summary>
    /// Exports OpenAPI component classes found in the specified assemblies
    /// into a compiled assembly.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for OpenAPI component classes.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The path to the compiled assembly containing the class definitions.</returns>
    public static string ExportOpenApiClasses(Assembly[] assemblies, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            var totalLoaded = AppDomain.CurrentDomain.GetAssemblies().Length;
            logger.Debug("Exporting OpenAPI component classes: scanning {AssemblyCount} assemblies (of {TotalLoaded} loaded)", assemblies.Length, totalLoaded);
        }

        // 1. Collect all component classes
        var componentTypes = assemblies
            .SelectMany(a => TryGetTypes(a, logger))
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(HasOpenApiComponentAttribute)
            .GroupBy(t => t.FullName ?? t.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Discovered {ComponentTypeCount} OpenAPI component type(s)", componentTypes.Count);
        }

        // For quick lookup when choosing type names
        var componentSet = new HashSet<Type>(componentTypes);

        // 2. Topologically sort by "uses other component as property type"
        var sorted = TopologicalSortByPropertyDependencies(componentTypes, componentSet);
        // nothing to export
        if (sorted.Count == 0)
        {
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("No OpenAPI component types found; exporter returning empty path");
            }
            return string.Empty;
        }
        // 3. Emit C# classes
        var source = GenerateCSharpSource(sorted, componentSet);

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Generated OpenAPI C# source for {ClassCount} type(s); length={SourceLength}", sorted.Count, source.Length);
        }

        // 4. Compile into a stable, cached DLL per runtime TFM
        return CompileToCachedAssembly(source, logger);
    }

    private static bool ShouldScanAssemblyForComponents(Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            // Dynamic assemblies are commonly used in tests and may contain runtime-defined components.
            return true;
        }

        var name = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Always include Kestrun assemblies.
        if (name.StartsWith("Kestrun", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Include user/app assemblies that reference Kestrun.Annotations (they can contain [OpenApiSchemaComponent] types).
        try
        {
            return assembly
                .GetReferencedAssemblies()
                .Any(r => string.Equals(r.Name, "Kestrun.Annotations", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<Type> TryGetTypes(Assembly assembly, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(assembly);
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger.Warning(ex, "Failed to load some types from assembly {AssemblyName}", assembly.FullName);
            return ex.Types.Where(t => t is not null)!;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to enumerate types from assembly {AssemblyName}", assembly.FullName);
            return [];
        }
    }

    private static string GenerateCSharpSource(IReadOnlyList<Type> sortedTypes, HashSet<Type> componentSet)
    {
        var sb = new StringBuilder();

        // NOTE: Hash excludes this header to keep the cache stable.
        _ = sb.AppendLine("// ================================================");
        _ = sb.AppendLine("//   Kestrun OpenAPI Autogenerated Class Definitions");
        _ = sb.AppendLine("//   DO NOT EDIT - generated at runtime");
        _ = sb.AppendLine("// ================================================");
        _ = sb.AppendLine();

        var classNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in sortedTypes)
        {
            // Skip types without full name (should not happen)
            if (type.FullName is null)
            {
                continue;
            }

            // Register the generated class identifier (global namespace).
            _ = classNames.Add(type.Name);

            AppendCSharpClass(type, componentSet, sb);
            _ = sb.AppendLine();
        }

        lock (ValidClassNamesLock)
        {
            ValidClassNames.Clear();
            ValidClassNames.AddRange(classNames);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines if the specified type has an OpenAPI component attribute.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private static bool HasOpenApiComponentAttribute(Type t)
    {
        return t.GetCustomAttributes(inherit: true)
                .Select(a => a.GetType().Name)
                .Any(n =>
                    n.Contains("OpenApiSchemaComponent", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("OpenApiRequestBodyComponent", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Appends the C# class definition for the specified type to the StringBuilder.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="componentSet"></param>
    /// <param name="sb"></param>
    private static void AppendCSharpClass(Type type, HashSet<Type> componentSet, StringBuilder sb)
    {
        // Detect base type (for parenting)
        var baseType = type.BaseType;
        var baseClause = string.Empty;

        if (baseType != null && baseType != typeof(object))
        {
            // Use C#-friendly type name for the base
            var baseCsName = ToCSharpTypeName(baseType, componentSet);
            baseClause = $" : {baseCsName}";
        }

        // Global namespace on purpose: PowerShell class FullName is typically unqualified.
        _ = sb.AppendLine($"public class {type.Name}{baseClause}");
        _ = sb.AppendLine("{");

        // Only properties *declared* on this type (no inherited ones)
        var props = type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var p in props)
        {
            var csType = ToCSharpTypeName(p.PropertyType, componentSet);
            _ = sb.AppendLine($"    public {csType} {p.Name} {{ get; set; }}");
        }

        _ = sb.AppendLine("}");
    }

    private static readonly IReadOnlyDictionary<Type, string> TypeAliases = new Dictionary<Type, string>
    {
        [typeof(long)] = "long",
        [typeof(int)] = "int",
        [typeof(bool)] = "bool",
        [typeof(string)] = "string",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(decimal)] = "decimal",
        [typeof(object)] = "object",
        [typeof(DateTime)] = "System.DateTime",
        [typeof(DateTimeOffset)] = "System.DateTimeOffset",
        [typeof(Guid)] = "System.Guid",
    };

    private static string ToCSharpTypeName(Type t, IReadOnlySet<Type> componentSet)
    {
        // Nullable<T>
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null)
        {
            var inner = ToCSharpTypeName(underlying, componentSet);
            return underlying.IsValueType ? $"{inner}?" : inner;
        }

        // Direct mappings (primitives, well-known types)
        if (TypeAliases.TryGetValue(t, out var alias))
        {
            return alias;
        }

        // Arrays
        if (t.IsArray)
        {
            var element = ToCSharpTypeName(t.GetElementType()!, componentSet);
            return $"{element}[]";
        }

        // OpenAPI component classes => simple name
        if (componentSet.Contains(t))
        {
            return t.Name;
        }

        // Generics
        if (t.IsGenericType)
        {
            return ToCSharpGenericTypeName(t, componentSet);
        }

        // Fallback
        return (t.FullName ?? t.Name).Replace('+', '.');
    }

    private static string ToCSharpGenericTypeName(Type t, IReadOnlySet<Type> componentSet)
    {
        // Generic type definition name (strip arity: `1, `2, ...)
        var def = t.GetGenericTypeDefinition();
        var rawName = (def.FullName ?? def.Name).Replace('+', '.');
        var tick = rawName.IndexOf('`');
        if (tick >= 0)
        {
            rawName = rawName[..tick];
        }

        var args = t.GetGenericArguments()
            .Select(a => ToCSharpTypeName(a, componentSet));

        return $"{rawName}<{string.Join(", ", args)}>";
    }

    /// <summary>
    /// Topologically sort types so that dependencies (property types)
    /// appear before the types that reference them.
    /// </summary>
    /// <param name="types">The list of types to sort.</param>
    /// <param name="componentSet">Set of component types for quick lookup.</param>
    /// <returns>The sorted list of types.</returns>
    private static List<Type> TopologicalSortByPropertyDependencies(
        List<Type> types,
        HashSet<Type> componentSet)
    {
        var result = new List<Type>();
        var visited = new Dictionary<Type, bool>(); // false = temp-mark, true = perm-mark

        foreach (var t in types)
        {
            Visit(t, componentSet, visited, result);
        }

        return result;
    }

    /// <summary>
    /// Visits the type and its dependencies recursively for topological sorting.
    /// </summary>
    /// <param name="t">Type to visit</param>
    /// <param name="componentSet">Set of component types</param>
    /// <param name="visited">Dictionary tracking visited types and their mark status</param>
    /// <param name="result">List to accumulate the sorted types</param>
    private static void Visit(Type t, HashSet<Type> componentSet, Dictionary<Type, bool> visited, List<Type> result)
    {
        if (visited.TryGetValue(t, out var perm))
        {
            if (!perm)
            {
                // cycle; ignore for now
                return;
            }
            return;
        }

        // temp-mark
        visited[t] = false;

        var deps = new List<Type>();

        // 1) Dependencies via property types (component properties)
        var propDeps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => GetComponentDependencyType(p.PropertyType, componentSet))
                        .Where(dep => dep is not null)
                        .Select(dep => dep!)
                        .Distinct();

        deps.AddRange(propDeps);

        // 2) Dependency via base type (parenting)
        var baseType = t.BaseType;
        if (baseType != null && componentSet.Contains(baseType))
        {
            deps.Add(baseType);
        }

        foreach (var dep in deps.Distinct())
        {
            Visit(dep, componentSet, visited, result);
        }

        // perm-mark
        visited[t] = true;
        result.Add(t);
    }

    /// <summary>
    /// Determines if the property type is a dependency on another component type.
    /// Unwraps Nullable and arrays to find the underlying type.
    /// </summary>
    /// <param name="propertyType">The property type to check.</param>
    /// <param name="componentSet">Set of component types for lookup.</param>
    /// <returns>The component type if it's a dependency; otherwise, null.</returns>
    private static Type? GetComponentDependencyType(Type propertyType, HashSet<Type> componentSet)
    {
        // Unwrap Nullable
        if (Nullable.GetUnderlyingType(propertyType) is Type underlying)
        {
            propertyType = underlying;
        }

        // Unwrap arrays
        if (propertyType.IsArray)
        {
            propertyType = propertyType.GetElementType()!;
        }

        return componentSet.Contains(propertyType) ? propertyType : null;
    }

    /// <summary>
    /// Compiles the C# source code into a cached assembly DLL.
    /// </summary>
    /// <param name="source">The C# source code to compile.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The path to the compiled assembly DLL.</returns>
    private static string CompileToCachedAssembly(string source, Serilog.ILogger logger)
    {
        var hashInput = StripLeadingCommentHeader(source);
        var hash = ComputeSha256Hex(hashInput);
        var tfm = GetRuntimeTfmMoniker();

        var outputDir = Path.Combine(Path.GetTempPath(), "Kestrun", "OpenApiClasses", tfm);
        var outputPath = Path.Combine(outputDir, hash + ".dll");

        var isDebug = logger.IsEnabled(Serilog.Events.LogEventLevel.Debug);

        if (isDebug)
        {
            logger.Debug("OpenAPI class assembly cache key: tfm={Tfm} sha256={Hash}", tfm, hash);
            logger.Debug("OpenAPI class assembly cache path: {Path}", outputPath);
        }

        // Fast path: already built
        if (File.Exists(outputPath))
        {
            if (isDebug)
            {
                logger.Debug("OpenAPI class assembly cache hit: {Path}", outputPath);
            }

            LoadIntoDefaultAssemblyLoadContextIfNeeded(outputPath, logger);
            return outputPath;
        }

        var mutexName = $"Global\\Kestrun.OpenApiClasses.{tfm}.{hash}";
        using var mutex = new Mutex(initiallyOwned: false, name: mutexName);

        var mutexHeld = false;
        try
        {
            mutexHeld = mutex.WaitOne(TimeSpan.FromSeconds(30));
            if (!mutexHeld)
            {
                logger.Warning(
                    "Timed out waiting for OpenAPI class assembly build mutex {MutexName}; proceeding best-effort",
                    mutexName);
            }

            // Re-check inside the lock/best-effort region.
            if (!File.Exists(outputPath))
            {
                _ = Directory.CreateDirectory(outputDir);
                CompileAndPublishCachedAssembly(source, outputPath, hash, logger, isDebug);
            }
            else if (isDebug)
            {
                logger.Debug("OpenAPI class assembly cache hit: {Path}", outputPath);
            }
        }
        finally
        {
            if (mutexHeld)
            {
                ReleaseMutexSafely(mutex, mutexName, logger, isDebug);
            }
        }

        LoadIntoDefaultAssemblyLoadContextIfNeeded(outputPath, logger);
        return outputPath;
    }

    /// <summary>
    /// Compiles the C# source code to a temporary DLL and publishes it atomically to the output path.
    /// </summary>
    /// <param name="source">The C# source code to compile.</param>
    /// <param name="outputPath">The final output path for the compiled DLL.</param>
    /// <param name="hash">The hash of the source code (used for assembly name).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="isDebug">Indicates if debug logging is enabled.</param>
    private static void CompileAndPublishCachedAssembly(
        string source,
        string outputPath,
        string hash,
        Serilog.ILogger logger,
        bool isDebug)
    {
        var tmpPath = outputPath + "." + Environment.ProcessId + ".tmp";

        try
        {
            logger.Information("Compiling OpenAPI component classes to cached DLL: {Path}", outputPath);
            CompileCSharpToDll(source, tmpPath, assemblyName: $"Kestrun.OpenApiClasses.{hash}");

            // Atomic publish.
            File.Move(tmpPath, outputPath);

            if (isDebug)
            {
                logger.Debug("Published OpenAPI class assembly: {Path}", outputPath);
            }
        }
        catch (IOException)
        {
            // Another process likely published first.
            if (isDebug)
            {
                logger.Debug(
                    "OpenAPI class assembly publish race detected; another process likely wrote {Path}",
                    outputPath);
            }

            TryDeleteTempFile(tmpPath, logger, isDebug);
        }
    }

    private static void TryDeleteTempFile(string tmpPath, Serilog.ILogger logger, bool isDebug)
    {
        try
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
        }
        catch
        {
            if (isDebug)
            {
                logger.Debug("Failed to delete temporary OpenAPI class assembly file: {Path}", tmpPath);
            }
        }
    }

    private static void ReleaseMutexSafely(Mutex mutex, string mutexName, Serilog.ILogger logger, bool isDebug)
    {
        try
        {
            mutex.ReleaseMutex();
        }
        catch
        {
            if (isDebug)
            {
                logger.Debug("Failed to release OpenAPI class assembly build mutex: {MutexName}", mutexName);
            }
        }
    }

    private static void LoadIntoDefaultAssemblyLoadContextIfNeeded(string assemblyPath, Serilog.ILogger logger)
    {
        try
        {
            var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => !a.IsDynamic &&
                         !string.IsNullOrWhiteSpace(a.Location) &&
                         string.Equals(a.Location, assemblyPath, StringComparison.OrdinalIgnoreCase));

            if (alreadyLoaded)
            {
                return;
            }

            // Load into Default ALC to keep a single type identity across the process.
            _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("Loaded OpenAPI class assembly into default ALC: {Path}", assemblyPath);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: runspaces may still load via InitialSessionState.Assemblies.
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug(ex, "Failed to load OpenAPI class assembly into default ALC: {Path}", assemblyPath);
            }
        }
    }

    private static string StripLeadingCommentHeader(string source)
    {
        // Only strip a contiguous comment header at the very top.
        // We intentionally do NOT attempt to remove comments elsewhere.
        using var reader = new StringReader(source);
        var sb = new StringBuilder();
        var skipping = true;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (skipping)
            {
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                skipping = false;
            }

            _ = sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static string ComputeSha256Hex(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string GetRuntimeTfmMoniker()
    {
        // Use the actual runtime version so we always compile against the current process TPA.
        // Example: 8.0.x -> net8.0, 10.0.x -> net10.0
        var v = Environment.Version;
        return $"net{v.Major}.0";
    }

    private static void CompileCSharpToDll(string source, string outputPath, string assemblyName)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = GetCompilationReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        var result = compilation.Emit(fs);

        if (!result.Success)
        {
            var diags = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException("Failed to compile OpenAPI classes assembly: " + string.Join(Environment.NewLine, diags));
        }
    }

    private static List<MetadataReference> GetCompilationReferences()
    {
        // The runtime TPA list covers the framework, but not app-specific assemblies.
        // Generated OpenAPI classes can reference types from Kestrun/Kestrun.Annotations (and other loaded libs),
        // so include all currently loaded, file-backed assemblies as Roslyn references.
        var refs = GetTrustedPlatformAssemblyReferences();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in refs.OfType<PortableExecutableReference>())
        {
            if (!string.IsNullOrWhiteSpace(r.FilePath))
            {
                _ = seenPaths.Add(r.FilePath);
            }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic)
            {
                continue;
            }

            var location = asm.Location;
            if (string.IsNullOrWhiteSpace(location) || !File.Exists(location))
            {
                continue;
            }

            if (seenPaths.Add(location))
            {
                refs.Add(MetadataReference.CreateFromFile(location));
            }
        }

        return refs;
    }

    private static List<MetadataReference> GetTrustedPlatformAssemblyReferences()
    {
        var refs = new List<MetadataReference>();
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(tpa))
        {
            return refs;
        }

        var paths = tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Some entries might not exist in constrained environments.
        foreach (var p in paths.Where(p => File.Exists(p)))
        {
            // Add as MetadataReference
            refs.Add(MetadataReference.CreateFromFile(p));
        }

        return refs;
    }
}

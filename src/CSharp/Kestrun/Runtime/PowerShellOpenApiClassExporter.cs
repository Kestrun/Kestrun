using System.Reflection;
using System.Text;

namespace Kestrun.Runtime;

/// <summary>
/// Exports OpenAPI component classes as PowerShell class definitions.
/// </summary>
public static class PowerShellOpenApiClassExporter
{
    /// <summary>
    /// Holds valid class names to be used as type in the OpenAPI function definitions.
    /// </summary>
    public static List<string> ValidClassNames { get; } = [];

    /// <summary>
    /// Exports OpenAPI component classes found in loaded assemblies
    /// as PowerShell class definitions.
    /// </summary>
    /// <returns>The path to the temporary PowerShell script containing the class definitions.</returns>
    public static string ExportOpenApiClasses()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
           .Where(a => a.FullName is not null &&
                    a.FullName.Contains("PowerShell Class Assembly"))
           .ToArray();
        return ExportOpenApiClasses(assemblies);
    }

    /// <summary>
    /// Exports OpenAPI component classes found in the specified assemblies
    /// as PowerShell class definitions
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for OpenAPI component classes.</param>
    /// <returns>The path to the temporary PowerShell script containing the class definitions.</returns>
    public static string ExportOpenApiClasses(Assembly[] assemblies)
    {
        // 1. Collect all component classes
        var componentTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(HasOpenApiComponentAttribute)
            .ToList();

        // For quick lookup when choosing type names
        var componentSet = new HashSet<Type>(componentTypes);

        // 2. Topologically sort by "uses other component as property type"
        var sorted = TopologicalSortByPropertyDependencies(componentTypes, componentSet);
        // nothing to export
        if (sorted.Count == 0)
        {
            return string.Empty;
        }
        // 3. Emit PowerShell classes
        var sb = new StringBuilder();

        foreach (var type in sorted)
        {
            // Skip types without full name (should not happen)
            if (type.FullName is null)
            {
                continue;
            }
            if (ValidClassNames.Contains(type.FullName))
            {
                // Already registered remove old entry
                _ = ValidClassNames.Remove(type.FullName);
            }
            // Register valid class name
            ValidClassNames.Add(type.FullName);
            // Emit class definition
            AppendClass(type, componentSet, sb);
            _ = sb.AppendLine(); // blank line between classes
        }
        // 4. Write to temp script file
        return WriteOpenApiTempScript(sb.ToString());
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
    /// Appends the PowerShell class definition for the specified type to the StringBuilder.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="componentSet"></param>
    /// <param name="sb"></param>
    private static void AppendClass(Type type, HashSet<Type> componentSet, StringBuilder sb)
    {
        // Detect base type (for parenting)
        var baseType = type.BaseType;
        var baseClause = string.Empty;

        if (baseType != null && baseType != typeof(object))
        {
            // Use PS-friendly type name for the base
            var basePsName = ToPowerShellTypeName(baseType, componentSet);
            baseClause = $" : {basePsName}";
        }

        _ = sb.AppendLine($"class {type.Name}{baseClause} {{");

        // Only properties *declared* on this type (no inherited ones)
        var props = type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var p in props)
        {
            var psType = ToPowerShellTypeName(p.PropertyType, componentSet);
            _ = sb.AppendLine($"    [{psType}]${p.Name}");
        }

        _ = sb.AppendLine("}");
    }

    /// <summary>
    /// Converts a .NET type to a PowerShell type name.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="componentSet"></param>
    /// <returns></returns>
    private static string ToPowerShellTypeName(Type t, HashSet<Type> componentSet)
    {
        // Nullable<T>
        if (Nullable.GetUnderlyingType(t) is Type underlying)
        {
            return $"Nullable[{ToPowerShellTypeName(underlying, componentSet)}]";
        }

        // Primitive mappings
        if (t == typeof(long))
        {
            return "long";
        }

        if (t == typeof(int))
        {
            return "int";
        }

        if (t == typeof(bool))
        {
            return "bool";
        }

        if (t == typeof(string))
        {
            return "string";
        }

        if (t == typeof(double))
        {
            return "double";
        }

        if (t == typeof(float))
        {
            return "single";
        }

        if (t == typeof(object))
        {
            return "object";
        }

        // Arrays
        if (t.IsArray)
        {
            var element = ToPowerShellTypeName(t.GetElementType()!, componentSet);
            return $"{element}[]";
        }

        // If the property type is itself one of the OpenAPI component classes,
        // use its *simple* name (Pet, User, Tag, Category, etc.)
        if (componentSet.Contains(t))
        {
            return t.Name;
        }

        // Fallback for other reference types (you can change to t.Name if you prefer)
        return t.FullName ?? t.Name;
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
    private static void Visit(
     Type t,
     HashSet<Type> componentSet,
     Dictionary<Type, bool> visited,
     List<Type> result)
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
    /// Writes the OpenAPI class definitions to a temporary PowerShell script file.
    /// </summary>
    /// <param name="openApiClasses">The OpenAPI class definitions as a string.</param>
    /// <returns>The path to the temporary PowerShell script file.</returns>
    public static string WriteOpenApiTempScript(string openApiClasses)
    {
        // Use a stable file name so multiple runspaces share the same script
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ps1");

        // Ensure directory exists
        _ = Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        // Build content with header
        var sb = new StringBuilder()
        .AppendLine("# ================================================")
        .AppendLine("#   Kestrun OpenAPI Autogenerated Class Definitions")
        .AppendLine("#   DO NOT EDIT - generated at runtime")
        .Append("#   Timestamp: ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")).Append('Z').AppendLine()
        .AppendLine("# ================================================")
        .AppendLine()
        .AppendLine(openApiClasses);

        // Save using UTF-8 without BOM
        File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return tempPath;
    }
}

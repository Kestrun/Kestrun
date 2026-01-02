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
    /// <param name="userCallbacks">Optional user-defined functions to include in the export.</param>
    /// <returns>The path to the temporary PowerShell script containing the class definitions.</returns>
    public static string ExportOpenApiClasses(Dictionary<string, string>? userCallbacks)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
           .Where(a => a.FullName is not null &&
                    a.FullName.Contains("PowerShell Class Assembly"))
           .ToArray();
        return ExportOpenApiClasses(assemblies: assemblies, userCallbacks: userCallbacks);
    }

    /// <summary>
    /// Exports OpenAPI component classes found in the specified assemblies
    /// as PowerShell class definitions
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for OpenAPI component classes.</param>
    ///  <param name="userCallbacks"> Optional user-defined functions to include in the export.</param>
    /// <returns>The path to the temporary PowerShell script containing the class definitions.</returns>
    public static string ExportOpenApiClasses(Assembly[] assemblies, Dictionary<string, string>? userCallbacks)
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
        var hasCallbacks = userCallbacks is not null && userCallbacks.Count > 0;

        // nothing to export
        if (sorted.Count == 0 && !hasCallbacks)
        {
            return string.Empty;
        }

        // 3. Emit PowerShell classes (and optional callback functions)
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

        if (hasCallbacks)
        {
            _ = sb.AppendLine("# ================================================");
            _ = sb.AppendLine("#   Kestrun User Callback Functions");
            _ = sb.AppendLine("# ================================================");
            _ = sb.AppendLine();

            foreach (var kvp in userCallbacks!.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var name = kvp.Key;
                var definition = kvp.Value ?? string.Empty;

                // Emit a standardized callback function wrapper:
                // - keeps parameter type constraints
                // - strips OpenAPI/Parameter attributes
                // - builds $params and calls $Context.Response.AddCallbackParameters(...)
                var functionScript = BuildCallbackFunctionStub(name, definition);
                var normalized = NormalizeBlankLines(functionScript);
                _ = sb.AppendLine(normalized);
                _ = sb.AppendLine();
            }
        }
        // 4. Write to temp script file
        return WriteOpenApiTempScript(sb.ToString());
    }

    /// <summary>
    /// Normalizes blank lines in the provided PowerShell script.
    /// </summary>
    /// <param name="script">The PowerShell script as a string.</param>
    /// <returns>A string with normalized blank lines.</returns>
    private static string NormalizeBlankLines(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return string.Empty;
        }

        // Normalize newlines first
        script = script.Replace("\r\n", "\n").Replace("\r", "\n");

        var lines = script.Split('\n');
        var sb = new StringBuilder(script.Length);

        for (var idx = 0; idx < lines.Length; idx++)
        {
            var line = lines[idx].TrimEnd();
            var isBlank = string.IsNullOrWhiteSpace(line);

            // For callback function export we want compact output:
            // drop ALL whitespace-only lines (attribute stripping leaves many single blank lines).
            if (!isBlank)
            {
                _ = sb.AppendLine(line);
            }
        }

        // Trim trailing newlines
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds a PowerShell function stub for a user-defined callback function.
    /// </summary>
    /// <param name="functionName"> The name of the callback function. </param>
    /// <param name="definition"> The PowerShell function definition as a string. </param>
    /// <returns>A string containing the standardized PowerShell function stub.</returns>
    private static string BuildCallbackFunctionStub(string functionName, string definition)
    {
        var (paramBlock, paramNames, bodyParamName) = TryExtractParamInfo(definition);

        // Fall back to a no-param function if we can't parse anything.
        var strippedParamBlock = StripPowerShellAttributeBlocks(paramBlock);
        strippedParamBlock = NormalizeBlankLines(strippedParamBlock);

        // Ensure we always have a param(...) block for consistent output.
        if (string.IsNullOrWhiteSpace(strippedParamBlock))
        {
            strippedParamBlock = "param()";
            paramNames = [];
        }

        var sb = new StringBuilder();
        _ = sb.AppendLine($"function {functionName} {{");

        // Normalize indentation:
        // - "param(" line: 4 spaces
        // - parameter lines: 8 spaces
        // - closing ")": 4 spaces
        foreach (var rawLine in strippedParamBlock.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
        {
            var l = rawLine.Trim();
            if (l.Length == 0)
            {
                continue;
            }

            if (l.Equals(")", StringComparison.Ordinal))
            {
                _ = sb.Append("    ").AppendLine(l);
                continue;
            }

            if (l.StartsWith("param", StringComparison.OrdinalIgnoreCase))
            {
                _ = sb.Append("    ").AppendLine(l);
                continue;
            }

            _ = sb.Append("        ").AppendLine(l);
        }

        _ = sb.AppendLine("    $FunctionName = $MyInvocation.MyCommand.Name");
        _ = sb.AppendLine("    if ($null -eq $Context -or $null -eq $Context.Response) {");
        _ = sb.AppendLine("        if (Test-KrLogger) {");
        _ = sb.AppendLine("            Write-KrLog -Level Warning -Message '{function} must be called inside a route script with Callback enabled.' -Values $FunctionName");
        _ = sb.AppendLine("        } else {");
        _ = sb.AppendLine("            Write-Warning -Message \"$FunctionName must be called inside a route script with Callback enabled.\"");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        return");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("    Write-KrLog -Level Information -Message 'Defined callback function {CallbackFunction}' -Values $FunctionName");
        _ = sb.AppendLine("    $params = [System.Collections.Generic.Dictionary[string, object]]::new()");

        foreach (var p in paramNames)
        {
            // Use the exact casing captured from the param block; dictionary keys are case-insensitive in C#.
            _ = sb.AppendLine($"    $params['{p}'] = ${p}");
        }

        _ = sb.AppendLine(bodyParamName is { Length: > 0 }
            ? $"    $bodyParameterName = '{bodyParamName}'"
            : "    $bodyParameterName = $null");

        _ = sb.AppendLine();
        _ = sb.AppendLine("    $Context.Response.AddCallbackParameters(");
        _ = sb.AppendLine("        $MyInvocation.MyCommand.Name,");
        _ = sb.AppendLine("        $bodyParameterName,");
        _ = sb.AppendLine("        $params)");
        _ = sb.AppendLine("}");

        return sb.ToString();
    }

    private static (string ParamBlock, List<string> ParamNames, string? BodyParamName) TryExtractParamInfo(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            return (string.Empty, [], null);
        }

        // Try to isolate the param(...) block from a FunctionInfo.Definition string.
        var paramBlock = ExtractPowerShellParamBlock(definition);
        if (string.IsNullOrWhiteSpace(paramBlock))
        {
            return (string.Empty, [], null);
        }

        // Identify the request body parameter name (prefer OpenApiRequestBody attribute if present)
        // Example: [OpenApiRequestBody(...)] [PaymentStatusChangedEvent]$Body
        var bodyParamName = ExtractBodyParameterName(paramBlock);

        // Strip attribute blocks so we keep only type constraints + $paramName
        var stripped = StripPowerShellAttributeBlocks(paramBlock);
        var paramNames = ExtractParamNamesFromStrippedParamBlock(stripped);

        // If we didn't find OpenApiRequestBody, default to Body if present.
        if (string.IsNullOrWhiteSpace(bodyParamName) && paramNames.Any(p => string.Equals(p, "Body", StringComparison.OrdinalIgnoreCase)))
        {
            bodyParamName = paramNames.First(p => string.Equals(p, "Body", StringComparison.OrdinalIgnoreCase));
        }

        return (paramBlock, paramNames, bodyParamName);
    }

    /// <summary>
    /// States for scanning PowerShell script for quoted segments.
    /// </summary>
    private enum ScanState
    {
        /// <summary>
        /// Normal scanning state (not inside quotes).
        /// </summary>
        Normal,
        /// <summary>
        /// Inside single-quoted string segment.
        /// </summary>
        SingleQuoted,
        /// <summary>
        /// Inside double-quoted string segment.
        /// </summary>
        DoubleQuoted
    }

    /// <summary>
    /// Extracts the parameter block from a PowerShell function definition.
    /// </summary>
    /// <param name="definition"> The PowerShell function definition string. </param>
    /// <returns>The parameter block string including the 'param(...)' syntax; or an empty string if not found.</returns>
    private static string ExtractPowerShellParamBlock(string definition)
    {
        if (string.IsNullOrEmpty(definition))
        {
            return string.Empty;
        }

        var idx = definition.IndexOf("param", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return string.Empty;
        }

        var open = definition.IndexOf('(', idx);
        if (open < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        var state = ScanState.Normal;

        for (var i = open; i < definition.Length; i++)
        {
            if (TryConsumeQuoted(definition, ref i, ref state))
            {
                continue;
            }

            var ch = definition[i];

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return definition.Substring(idx, i - idx + 1);
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Tries to consume a quoted segment in the PowerShell script.
    /// </summary>
    /// <param name="s"> The input string to scan. </param>
    /// <param name="i"> The current index in the string, passed by reference and updated as the quoted segment is consumed. </param>
    /// <param name="state"> The current scanning state, passed by reference and updated based on quote handling. </param>
    /// <returns>True if a quoted segment was consumed; otherwise, false.</returns>
    private static bool TryConsumeQuoted(string s, ref int i, ref ScanState state)
    {
        var ch = s[i];

        // Enter quote states
        if (state == ScanState.Normal)
        {
            if (ch == '\'') { state = ScanState.SingleQuoted; return true; }
            if (ch == '"') { state = ScanState.DoubleQuoted; return true; }
            return false;
        }

        // Inside single quotes: '' is an escaped single quote
        if (state == ScanState.SingleQuoted)
        {
            if (ch == '\'' && i + 1 < s.Length && s[i + 1] == '\'')
            {
                i++; // consume second '
                return true;
            }

            if (ch == '\'')
            {
                state = ScanState.Normal;
            }

            return true;
        }

        // Inside double quotes: backtick escapes the next char
        if (state == ScanState.DoubleQuoted)
        {
            if (ch == '`' && i + 1 < s.Length)
            {
                i++; // skip escaped char
                return true;
            }

            if (ch == '"')
            {
                state = ScanState.Normal;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the name of the body parameter from the parameter block, if annotated with [OpenApiRequestBody].
    /// </summary>
    /// <param name="paramBlock"> The parameter block string to search within. </param>
    /// <returns>The name of the body parameter if found; otherwise, null.</returns>
    private static string? ExtractBodyParameterName(string paramBlock)
    {
        // Very targeted heuristic: if [OpenApiRequestBody(...)] is present, pick the following $name.
        // This keeps the exporter decoupled from PowerShell AST dependencies.
        var marker = "OpenApiRequestBody";
        var idx = paramBlock.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        // Search forward for '$' then capture identifier
        for (var i = idx; i < paramBlock.Length; i++)
        {
            if (paramBlock[i] != '$')
            {
                continue;
            }

            var start = i + 1;
            var end = start;
            while (end < paramBlock.Length)
            {
                var ch = paramBlock[end];
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    break;
                }
                end++;
            }

            if (end > start)
            {
                return paramBlock[start..end];
            }
        }

        return null;
    }

    private static List<string> ExtractParamNamesFromStrippedParamBlock(string strippedParamBlock)
    {
        // Parse variable names only from within param(...)
        // We expect declarations like: [string]$paymentId,
        if (string.IsNullOrWhiteSpace(strippedParamBlock))
        {
            return [];
        }

        var names = new List<string>();
        var s = strippedParamBlock;

        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '$')
            {
                continue;
            }

            var start = i + 1;
            var end = start;
            if (start >= s.Length)
            {
                continue;
            }

            if (!(char.IsLetter(s[start]) || s[start] == '_'))
            {
                continue;
            }

            end++;
            while (end < s.Length)
            {
                var ch = s[end];
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    break;
                }
                end++;
            }

            var name = s[start..end];
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }

            i = end - 1;
        }

        return names;
    }

    private static string StripPowerShellAttributeBlocks(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(script.Length);
        var i = 0;
        while (i < script.Length)
        {
            var ch = script[i];
            if (ch != '[')
            {
                _ = sb.Append(ch);
                i++;
                continue;
            }

            // Capture a full bracket block, handling nested [ ... ] (e.g. generic type constraints)
            var start = i;
            var depth = 0;
            var j = i;
            while (j < script.Length)
            {
                var cj = script[j];
                if (cj == '[')
                {
                    depth++;
                }
                else if (cj == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        j++; // include closing ']'
                        break;
                    }
                }
                j++;
            }

            // If unbalanced, just emit the rest
            if (depth != 0)
            {
                _ = sb.Append(script.AsSpan(i));
                break;
            }

            var block = script.AsSpan(start, j - start);

            // Attribute blocks always include parentheses in our usage (e.g. [OpenApiPath(...)], [Parameter()]).
            // Keep type constraints like [string], [int], [MyType], [MyType[]], [List[string]].
            if (block.IndexOf('(') >= 0)
            {
                i = j;
                continue;
            }

            _ = sb.Append(block);
            i = j;
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
            var basePsName = ToPowerShellTypeName(baseType, componentSet, collapseOpenApiValueTypes: false);
            baseClause = $" : {basePsName}";
        }
        _ = sb.AppendLine("[NoRunspaceAffinity()]");
        _ = sb.AppendLine($"class {type.Name}{baseClause} {{");

        // Only properties *declared* on this type (no inherited ones)
        var props = type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var p in props)
        {
            var psType = ToPowerShellTypeName(p.PropertyType, componentSet, collapseOpenApiValueTypes: true);
            _ = sb.AppendLine($"    [{psType}]${p.Name}");
        }

        _ = sb.AppendLine("}");
    }

    /// <summary>
    /// Converts a .NET type to a PowerShell type name.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="componentSet"></param>
    /// <param name="collapseOpenApiValueTypes">When true, types derived from OpenApiValue&lt;T&gt; are emitted as their underlying primitive (e.g., string/double/bool/long).</param>
    /// <returns></returns>
    private static string ToPowerShellTypeName(Type t, HashSet<Type> componentSet, bool collapseOpenApiValueTypes)
    {
        // Nullable<T>
        if (Nullable.GetUnderlyingType(t) is Type underlying)
        {
            return $"Nullable[{ToPowerShellTypeName(underlying, componentSet, collapseOpenApiValueTypes)}]";
        }

        // OpenAPI schema component array wrappers:
        // Some PowerShell OpenAPI schemas are modeled as a component class with Array=$true,
        // typically inheriting from the element schema type (e.g. EventDates : Date).
        // When referenced as a property type, we want the PowerShell type constraint to be
        // the element array (e.g. [Date[]]) instead of the wrapper class ([EventDates]).
        // IMPORTANT: this must run before OpenApiValue<T> collapsing so wrappers don't lose their array-ness.
        if (collapseOpenApiValueTypes && componentSet.Contains(t) && TryGetArrayComponentElementType(t, out var elementType) && elementType is not null)
        {
            // Guard against pathological self-references.
            if (elementType == t)
            {
                return t.Name;
            }

            var elementPsName = ToPowerShellTypeName(elementType, componentSet, collapseOpenApiValueTypes);
            return $"{elementPsName}[]";
        }

        // OpenAPI primitive wrappers (PowerShell-friendly):
        // Many schemas are represented as classes deriving from OpenApiValue<T>
        // (e.g. OpenApiString/OpenApiNumber/OpenApiBoolean/OpenApiInteger, OpenApiDate, etc.).
        // When such a schema is referenced as a property type, we want the *real*
        // PowerShell primitive type constraint (string/double/bool/long) rather than
        // the wrapper class name.
        if (collapseOpenApiValueTypes && TryGetOpenApiValueUnderlyingType(t, out var openApiValueUnderlying) && openApiValueUnderlying is not null)
        {
            return ToPowerShellTypeName(openApiValueUnderlying, componentSet, collapseOpenApiValueTypes);
        }

        // Primitive mappings
        if (ResolvePrimitiveTypeName(t) is string primitiveName)
        {
            return primitiveName;
        }

        // Arrays
        if (t.IsArray)
        {
            var element = ToPowerShellTypeName(t.GetElementType()!, componentSet, collapseOpenApiValueTypes);
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
    /// Resolves the PowerShell type name for common .NET primitive types.
    /// </summary>
    /// <param name="t">The .NET type to resolve.</param>
    /// <returns>The PowerShell type name if the type is a recognized primitive; otherwise, null.</returns>
    private static string? ResolvePrimitiveTypeName(Type t)
    {
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
        return null;
    }
    private static bool TryGetOpenApiValueUnderlyingType(Type t, out Type? underlyingType)
    {
        underlyingType = null;

        // Walk base types looking for OpenApiValue<T> (by name to avoid hard coupling).
        // OpenApiValue<T> lives in Kestrun.Annotations and is in the global namespace.
        var current = t;

        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType)
            {
                var def = current.GetGenericTypeDefinition();
                if (string.Equals(def.Name, "OpenApiValue`1", StringComparison.Ordinal))
                {
                    underlyingType = current.GetGenericArguments()[0];
                    return true;
                }
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool TryGetArrayComponentElementType(Type componentType, out Type? elementType)
    {
        elementType = null;

        // We don't take a hard dependency on the annotation type here; this exporter
        // may reflect PowerShell-generated assemblies. We detect the attribute by name
        // and then read common properties via reflection.
        var attr = componentType
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name.Contains("OpenApiSchemaComponent", StringComparison.OrdinalIgnoreCase));

        if (attr is null)
        {
            return false;
        }

        var attrType = attr.GetType();
        var arrayProp = attrType.GetProperty("Array");
        if (arrayProp?.GetValue(attr) is not bool isArray || !isArray)
        {
            return false;
        }

        // Prefer explicit ItemsType if provided.
        var itemsTypeProp = attrType.GetProperty("ItemsType");
        if (itemsTypeProp?.GetValue(attr) is Type itemsType)
        {
            elementType = itemsType;
            return true;
        }

        // Common PowerShell pattern: wrapper inherits from element schema.
        var baseType = componentType.BaseType;
        if (baseType is not null && baseType != typeof(object))
        {
            elementType = baseType;
            return true;
        }

        return false;
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
        .AppendLine("[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSProvideCommentHelp', '')]")
        .AppendLine("param()")
        .AppendLine(openApiClasses);

        // Save using UTF-8 without BOM
        File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return tempPath;
    }
}

using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Collections;
using System.Management.Automation.Internal;


namespace Kestrun.OpenApi;

/// <summary>
/// Scans PowerShell script files for OpenAPI component annotations defined via attributes.
/// </summary>
public static class OpenApiComponentAnnotationScanner
{
    /// <summary>
    /// Represents a variable discovered in script, along with its OpenAPI annotations and metadata.
    /// </summary>
    public sealed class AnnotatedVariable
    {
        /// <summary>Annotations attached to the variable.</summary>
        public List<KestrunAnnotation> Annotations { get; } = [];

        /// <summary>The declared variable type if present (e.g. from <c>[int]$x</c> or <c>[int]$x = 1</c>).</summary>
        public Type? VariableType { get; set; }

        /// <summary>The declared variable type name as written in script (best-effort).</summary>
        public string? VariableTypeName { get; set; }

        /// <summary>The initializer value if it can be evaluated (best-effort).</summary>
        public object? InitialValue { get; set; }

        /// <summary>The initializer expression text (always available when an initializer exists).</summary>
        public string? InitialValueExpression { get; set; }
        /// <summary>Indicates whether the variable was declared with no default (e.g. <c>$x = [NoDefault]</c>).</summary>
        public bool NoDefault { get; internal set; }
    }

    /// <summary>
    /// Scan starting from the running script (via $PSCommandPath) or a provided mainPath,
    /// follow dot-sourced files, and extract "standalone attribute line applies to next variable assignment" annotations.
    /// </summary>
    /// <param name="engine">The PowerShell engine intrinsics.</param>
    /// <param name="mainPath">Optional main script file path to start scanning from. If null, uses the running script path ($PSCommandPath).</param>
    /// <param name="attributeTypeFilter">Optional filter of attribute type names to include. If null or empty, includes all.</param>
    /// <param name="componentNameArgument">The name of the attribute argument to use as component name.</param>
    /// <param name="maxFiles">Maximum number of files to scan to prevent cycles.</param>
    /// <exception cref="InvalidOperationException">Thrown if no running script path is found and no mainPath is provided.</exception>
    /// <returns>
    /// A dictionary keyed by variable name. Each value contains the collected annotations,
    /// the declared variable type (if any), and the initializer value/expression (if any).
    /// </returns>
    /// <remarks>
    /// This method performs a breadth-first traversal of the script files starting from mainPath or $PSCommandPath,
    /// following dot-sourced includes and extracting attribute annotations that precede variable assignments.
    /// </remarks>
    public static Dictionary<string, AnnotatedVariable> ScanFromRunningScriptOrPath(
        EngineIntrinsics engine,
        string? mainPath = null,
        IReadOnlyCollection<string>? attributeTypeFilter = null,
        string componentNameArgument = "Name",
        int maxFiles = 200)
    {
        // Prefer running script path if available
        var psCommandPath = engine.SessionState.PSVariable.GetValue("PSCommandPath") as string;
        var entry = mainPath ?? psCommandPath;

        if (string.IsNullOrWhiteSpace(entry))
        {
            throw new InvalidOperationException("No running script path found ($PSCommandPath is empty) and no mainPath provided.");
        }

        entry = Path.GetFullPath(entry);

        return ScanFromPath(entry, attributeTypeFilter, componentNameArgument, maxFiles);
    }

    /// <summary>
    /// Scan starting from a main script file path.
    /// </summary>
    /// <param name="mainPath">The main script file path to start scanning from.</param>
    /// <param name="attributeTypeFilter">Optional filter of attribute type names to include. If null or empty, includes all.</param>
    /// <param name="componentNameArgument">The name of the attribute argument to use as component name.</param>
    /// <param name="maxFiles">Maximum number of files to scan to prevent cycles.</param>
    /// <returns>
    /// A dictionary keyed by variable name. Each value contains the collected annotations,
    /// the declared variable type (if any), and the initializer value/expression (if any).
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the maximum number of files is exceeded while following dot-sourced scripts.</exception>
    /// <remarks>
    /// This method performs a breadth-first traversal of the script files starting from mainPath,
    /// following dot-sourced includes and extracting attribute annotations that precede variable assignments.
    /// </remarks>
    public static Dictionary<string, AnnotatedVariable> ScanFromPath(
        string mainPath,
        IReadOnlyCollection<string>? attributeTypeFilter = null,
        string componentNameArgument = "Name",
        int maxFiles = 200)
    {
        mainPath = Path.GetFullPath(mainPath);

        var variables = new Dictionary<string, AnnotatedVariable>(StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        _ = visited.Add(mainPath);
        queue.Enqueue(mainPath);

        while (queue.Count > 0)
        {
            if (visited.Count > maxFiles)
            {
                throw new InvalidOperationException($"Exceeded maxFiles={maxFiles} while following dot-sourced scripts. Possible cycle.");
            }

            var file = queue.Dequeue();
            var ast = ParseFile(file, out _);

            // If you care, you can log parse errors:
            // foreach (var e in errors) Console.Error.WriteLine($"{file}:{e.Extent.StartLineNumber}:{e.Message}");

            ExtractAnnotationsFromAst(ast, variables, attributeTypeFilter, componentNameArgument);

            foreach (var inc in FindDotSourcedFiles(ast, file))
            {
                if (inc is null)
                {
                    continue;
                }

                if (visited.Add(inc))
                {
                    queue.Enqueue(inc);
                }
            }
        }

        return variables;
    }

    // ---------------- Parsing ----------------

    /// <summary>
    /// Parses a PowerShell script file into a ScriptBlockAst.
    /// </summary>
    /// <param name="path">The file path of the PowerShell script to parse.</param>
    /// <param name="errors">Output array of parse errors encountered during parsing.</param>
    /// <returns>The parsed ScriptBlockAst representing the script's abstract syntax tree.</returns>
    private static ScriptBlockAst ParseFile(string path, out ParseError[] errors)
    {
        var text = File.ReadAllText(path);
        var ast = Parser.ParseInput(text, out _, out errors);
        // NOTE: ast.Extent.File is often null here; we carry file path ourselves when resolving includes.
        return ast;
    }

    // ---------------- Dot-sourcing discovery ----------------

    /// <summary>
    /// Finds dot-sourcing statements: . ./file.ps1, . "$PSScriptRoot\file.ps1", . ".\file.ps1"
    /// Best-effort: resolves simple literal/expandable strings only.
    /// </summary>
    /// <param name="ast">The ScriptBlockAst to search for dot-sourcing commands.</param>
    /// <param name="currentFilePath">The current script file path to resolve relative includes.</param>
    /// <returns>An enumerable of resolved file paths that are dot-sourced.</returns>
    private static IEnumerable<string?> FindDotSourcedFiles(ScriptBlockAst ast, string currentFilePath)
    {
        var baseDir = Path.GetDirectoryName(currentFilePath) ?? Directory.GetCurrentDirectory();

        var commands = ast.FindAll(n => n is CommandAst, searchNestedScriptBlocks: true)
                          .Cast<CommandAst>();

        foreach (var cmd in commands)
        {
            var elems = cmd.CommandElements;
            if (elems.Count < 2)
            {
                continue;
            }

            var first = elems[0].Extent.Text.Trim();
            if (first != ".")
            {
                continue;
            }

            var raw = TryGetSimpleString(elems[1]);
            if (raw is null)
            {
                continue;
            }

            var resolved = ResolveDotSourcedPath(raw, baseDir);
            if (resolved is not null)
            {
                yield return resolved;
            }
        }
    }

    /// <summary>
    /// Tries to extract a simple string value from a CommandElementAst.
    /// </summary>
    /// <param name="element">The CommandElementAst to extract the string from.</param>
    /// <returns>The extracted string if successful; otherwise, null.</returns>
    private static string? TryGetSimpleString(CommandElementAst element)
    {
        // Handles: ". './a.ps1'" or ". \"$PSScriptRoot\\a.ps1\""
        return element switch
        {
            StringConstantExpressionAst s => s.Value,
            ExpandableStringExpressionAst e => e.Value,// contains "$PSScriptRoot\foo.ps1" as text; we do best-effort replacement.
            _ => null,
        };
    }

    /// <summary>
    /// Resolves a dot-sourced file path, handling common tokens and relative paths.
    /// </summary>
    /// <param name="raw">The raw file path string from the dot-sourcing statement.</param>
    /// <param name="baseDir">The base directory to resolve relative paths against.</param>
    /// <returns>The resolved full file path if it exists; otherwise, null.</returns>
    private static string? ResolveDotSourcedPath(string raw, string baseDir)
    {
        var t = raw.Trim();

        // Expand the common tokens best-effort (no general expression evaluation!)
        t = t.Replace("$PSScriptRoot", baseDir, StringComparison.OrdinalIgnoreCase);
        t = t.Replace("$PWD", Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase);

        // Relative -> baseDir
        if (!Path.IsPathRooted(t))
        {
            t = Path.Combine(baseDir, t);
        }

        try
        {
            var full = Path.GetFullPath(t);
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    // ---------------- Annotation extraction ----------------
    /// <summary>
    /// Extracts attribute annotations from a ScriptBlockAst and populates the provided dictionary.
    /// </summary>
    /// <param name="scriptAst">The ScriptBlockAst to extract annotations from.</param>
    /// <param name="variables">The dictionary to populate with extracted annotations.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    /// <param name="strict">If true, clears pending annotations on non-matching statements.</param>
    private static void ExtractAnnotationsFromAst(
        ScriptBlockAst scriptAst,
        Dictionary<string, AnnotatedVariable> variables,
        IReadOnlyCollection<string>? attributeTypeFilter,
        string componentNameArgument,
        bool strict = true)
    {
        // We want statements in lexical order; easiest is to walk each NamedBlock and sort by offsets.
        var blocks = new List<Ast>();
        if (scriptAst.BeginBlock is not null)
        {
            blocks.Add(scriptAst.BeginBlock);
        }

        if (scriptAst.ProcessBlock is not null)
        {
            blocks.Add(scriptAst.ProcessBlock);
        }

        if (scriptAst.EndBlock is not null)
        {
            blocks.Add(scriptAst.EndBlock);
        }

        foreach (var block in blocks)
        {
            var statements = block.FindAll(n => n is StatementAst sa && sa.Parent == block, searchNestedScriptBlocks: false)
                                  .Cast<StatementAst>()
                                  .OrderBy(s => s.Extent.StartOffset)
                                  .ToList();

            var pending = new List<AttributeAst>();

            foreach (var st in statements)
            {
                // Inline attributed assignment, e.g.
                //   [OpenApiParameterComponent(...)]
                //   [int]$limit = 20
                // PowerShell can parse this as a single AssignmentStatementAst with attributes on the LHS expression.
                if (st is AssignmentStatementAst inlineAssign &&
                    TryExtractInlineAttributedAssignment(inlineAssign, attributeTypeFilter, out var inlineVarName, out var inlineVarType, out var inlineVarTypeName, out var inlineAttrs))
                {
                    var (initValue, initExpr) = EvaluateValueStatement(inlineAssign.Right);
                    var entry = GetOrCreateVariable(variables, inlineVarName);
                    entry.VariableType ??= inlineVarType;
                    entry.VariableTypeName ??= inlineVarTypeName;

                    if (initExpr != "NoDefault")
                    {
                        entry.NoDefault = false;
                        entry.InitialValue ??= initValue;
                        entry.InitialValueExpression ??= initExpr;
                    }
                    else
                    {
                        entry.NoDefault = true;
                    }
                    foreach (var a in inlineAttrs)
                    {
                        var ka = TryCreateAnnotation(a, defaultComponentName: inlineVarName, componentNameArgument);
                        if (ka is not null)
                        {
                            entry.Annotations.Add(ka);
                        }
                    }

                    pending.Clear();
                    continue;
                }

                // Inline attributed declaration, e.g.
                //   [OpenApiParameterComponent(...)]
                //   [int]$limit
                // PowerShell parses this as a single attributed expression statement.
                if (TryExtractInlineAttributedDeclaration(st, attributeTypeFilter, out var varName, out var varType, out var varTypeName, out var attrs))
                {
                    var entry = GetOrCreateVariable(variables, varName);
                    entry.VariableType ??= varType;
                    entry.VariableTypeName ??= varTypeName;

                    foreach (var a in attrs)
                    {
                        var ka = TryCreateAnnotation(a, defaultComponentName: varName, componentNameArgument);
                        if (ka is not null)
                        {
                            entry.Annotations.Add(ka);
                        }
                    }

                    pending.Clear();
                    continue;
                }

                var parsedAttrs = TryParseStandaloneAttributeLine(st.Extent.Text);
                if (parsedAttrs.Count > 0)
                {
                    foreach (var a in parsedAttrs)
                    {
                        if (IsMatchingAttribute(a, attributeTypeFilter))
                        {
                            pending.Add(a);
                        }
                    }
                    continue;
                }


                // Variable assignment:
                // - If we have pending annotations, attach them here.
                // - If the variable was already discovered via inline attributed declaration, also capture type/initializer here.
                if (st is AssignmentStatementAst assign &&
                    TryGetAssignmentTarget(assign.Left, out var targetVarName, out var targetVarType, out var targetVarTypeName))
                {
                    var shouldCapture = pending.Count > 0 || variables.ContainsKey(targetVarName);
                    if (shouldCapture)
                    {
                        var (initValue, initExpr) = EvaluateValueStatement(assign.Right);
                        var entry = GetOrCreateVariable(variables, targetVarName);
                        entry.VariableType ??= targetVarType;
                        entry.VariableTypeName ??= targetVarTypeName;
                        entry.InitialValue ??= initValue;
                        entry.InitialValueExpression ??= initExpr;

                        if (pending.Count > 0)
                        {
                            foreach (var a in pending)
                            {
                                var ka = TryCreateAnnotation(a, defaultComponentName: targetVarName, componentNameArgument);
                                if (ka is not null)
                                {
                                    entry.Annotations.Add(ka);
                                }
                            }

                            pending.Clear();
                        }

                        continue;
                    }
                }
                // Declaration-only variable (no assignment), e.g. [int]$param1
                if (pending.Count > 0 && st is CommandExpressionAst declExpr)
                {
                    if (TryGetDeclaredVariableInfo(declExpr.Expression, out var declaredVarName, out var declaredVarType, out var declaredVarTypeName))
                    {
                        var entry = GetOrCreateVariable(variables, declaredVarName);
                        entry.VariableType ??= declaredVarType;
                        entry.VariableTypeName ??= declaredVarTypeName;

                        foreach (var a in pending)
                        {
                            var ka = TryCreateAnnotation(a, defaultComponentName: declaredVarName, componentNameArgument);
                            if (ka is not null)
                            {
                                entry.Annotations.Add(ka);
                            }
                        }

                        pending.Clear();
                        continue;
                    }
                }


                // strict: anything else clears pending
                if (strict && pending.Count > 0)
                {
                    pending.Clear();
                }
            }

        }
    }

    private static AnnotatedVariable GetOrCreateVariable(Dictionary<string, AnnotatedVariable> variables, string varName)
    {
        if (!variables.TryGetValue(varName, out var entry))
        {
            entry = new AnnotatedVariable();
            variables[varName] = entry;
        }

        return entry;
    }

    private static bool TryExtractInlineAttributedDeclaration(
        StatementAst statement,
        IReadOnlyCollection<string>? attributeTypeFilter,
        out string variableName,
        out Type? variableType,
        out string? variableTypeName,
        out IReadOnlyList<AttributeAst> attributes)
    {
        variableName = string.Empty;
        variableType = null;
        variableTypeName = null;
        attributes = [];

        var expr = statement switch
        {
            CommandExpressionAst ce => ce.Expression,
            PipelineAst p when p.PipelineElements is { Count: 1 } && p.PipelineElements[0] is CommandExpressionAst ce => ce.Expression,
            _ => null
        };

        if (expr is null)
        {
            return false;
        }

        // Collect matching attributes from the attributed-expression chain.
        var found = new List<AttributeAst>();
        var cursor = expr;
        while (cursor is AttributedExpressionAst aex)
        {
            if (aex.Attribute is AttributeAst attr && IsMatchingAttribute(attr, attributeTypeFilter))
            {
                found.Add(attr);
            }
            cursor = aex.Child;
        }

        if (found.Count == 0)
        {
            return false;
        }

        if (!TryGetDeclaredVariableInfo(expr, out variableName, out variableType, out variableTypeName))
        {
            return false;
        }

        attributes = found;
        return true;
    }

    private static bool TryExtractInlineAttributedAssignment(
        AssignmentStatementAst assignment,
        IReadOnlyCollection<string>? attributeTypeFilter,
        out string variableName,
        out Type? variableType,
        out string? variableTypeName,
        out IReadOnlyList<AttributeAst> attributes)
    {
        variableName = string.Empty;
        variableType = null;
        variableTypeName = null;
        attributes = [];

        // Collect matching attributes from the left-hand attributed-expression chain.
        var found = new List<AttributeAst>();
        var cursor = assignment.Left;
        while (cursor is AttributedExpressionAst aex)
        {
            if (aex.Attribute is AttributeAst attr && IsMatchingAttribute(attr, attributeTypeFilter))
            {
                found.Add(attr);
            }
            cursor = aex.Child;
        }

        if (found.Count == 0)
        {
            return false;
        }

        if (!TryGetDeclaredVariableInfo(assignment.Left, out variableName, out variableType, out variableTypeName))
        {
            return false;
        }

        attributes = found;
        return true;
    }

    private static bool TryGetAssignmentTarget(ExpressionAst left, out string variableName, out Type? variableType, out string? variableTypeName)
        // Assignment can be: $x = 1   or   [int]$x = 1
        => TryGetDeclaredVariableInfo(left, out variableName, out variableType, out variableTypeName);

    private static bool TryGetDeclaredVariableInfo(ExpressionAst expr, out string variableName, out Type? variableType, out string? variableTypeName)
    {
        variableName = string.Empty;
        variableType = null;
        variableTypeName = null;

        var cursor = expr;
        var attributedTypeNames = new List<string>();
        while (cursor is AttributedExpressionAst aex)
        {
            if (!string.IsNullOrWhiteSpace(aex.Attribute?.TypeName?.FullName))
            {
                attributedTypeNames.Add(aex.Attribute.TypeName.FullName);
            }
            cursor = aex.Child;
        }

        // At this point we may have a ConvertExpressionAst ([int]$x) or a VariableExpressionAst ($x)
        if (cursor is ConvertExpressionAst cex)
        {
            variableTypeName = cex.Type.TypeName.FullName;
            variableType = ResolvePowerShellTypeName(variableTypeName);
            cursor = cex.Child;
        }

        // There may still be attributes (e.g. nested convert) - unwrap again defensively
        while (cursor is AttributedExpressionAst aex2)
        {
            cursor = aex2.Child;
        }

        if (cursor is VariableExpressionAst v)
        {
            variableName = v.VariablePath.UserPath;

            // PowerShell sometimes represents type constraints like [Nullable[datetime]]$x as another
            // attributed-expression rather than a ConvertExpressionAst. If we didn't get a type above,
            // try to infer it from the last non-annotation attribute type name.
            if (variableType is null && variableTypeName is null && attributedTypeNames.Count > 0)
            {
                for (var i = attributedTypeNames.Count - 1; i >= 0; i--)
                {
                    var tn = attributedTypeNames[i];
                    var t = ResolvePowerShellTypeName(tn);
                    if (t is null)
                    {
                        continue;
                    }

                    if (typeof(KestrunAnnotation).IsAssignableFrom(t))
                    {
                        continue;
                    }

                    variableTypeName = tn;
                    variableType = t;
                    break;
                }
            }

            return !string.IsNullOrWhiteSpace(variableName);
        }

        return false;
    }

    private static Type? ResolvePowerShellTypeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Best-effort resolution via PowerShell's own type name resolver.
        // This handles common accelerators and PowerShell syntax like Nullable[datetime].
        try
        {
            var psType = new PSTypeName(name.Trim()).Type;
            if (psType is not null)
            {
                return psType;
            }
        }
        catch
        {
            // Ignore and fall back to our heuristic mapping.
        }

        // Common accelerators
        var lowered = name.Trim();
        return lowered.ToLowerInvariant() switch
        {
            "int" => typeof(int),
            "long" => typeof(long),
            "double" => typeof(double),
            "float" => typeof(float),
            "decimal" => typeof(decimal),
            "bool" => typeof(bool),
            "string" => typeof(string),
            "datetime" => typeof(DateTime),
            "guid" => typeof(Guid),
            "ipaddress" => typeof(System.Net.IPAddress),
            "hashtable" => typeof(Hashtable),
            "object" => typeof(object),
            _ => ResolveTypeFromName(NormalizePowerShellTypeName(lowered))
        };
    }

    private static string NormalizePowerShellTypeName(string name)
    {
        // Handle Nullable[T]
        if (name.StartsWith("nullable[", StringComparison.OrdinalIgnoreCase) && name.EndsWith(']'))
        {
            var inner = name[9..^1];
            var innerType = ResolvePowerShellTypeName(inner);
            if (innerType is not null && innerType.IsValueType)
            {
                return typeof(Nullable<>).MakeGenericType(innerType).FullName!;
            }
        }

        // Handle array syntax: datetime[]
        if (name.EndsWith("[]", StringComparison.Ordinal))
        {
            var inner = name[..^2];
            var innerType = ResolvePowerShellTypeName(inner);
            if (innerType is not null)
            {
                return innerType.MakeArrayType().FullName!;
            }
        }

        return name;
    }

    private static (object? Value, string? Expression) EvaluateValueStatement(StatementAst statement)
    {
        // RHS of assignment is a StatementAst. Try to extract a single expression from it.
        var expr = statement switch
        {
            CommandExpressionAst ce => ce.Expression,
            PipelineAst p when p.PipelineElements is { Count: 1 } && p.PipelineElements[0] is CommandExpressionAst ce => ce.Expression,
            _ => null
        };

        if (expr is null)
        {
            var raw = statement.Extent.Text.Trim();
            return (string.IsNullOrWhiteSpace(raw) ? null : raw, string.IsNullOrWhiteSpace(raw) ? null : raw);
        }

        var value = EvaluateArgumentExpression(expr);
        var text = expr.Extent.Text.Trim();
        return (value, string.IsNullOrWhiteSpace(text) ? null : text);
    }



    private static KestrunAnnotation? TryCreateAnnotation(
        AttributeAst attr,
        string defaultComponentName,
        string componentNameArgument)
    {
        var attribute = TryCreateKestrunAnnotation(attr, defaultComponentName, componentNameArgument);
        if (attribute is not null)
        {
            return attribute;
        }

        attribute = TryCmdletMetadataAttribute(attr);

        return attribute;

    }
    private static KestrunAnnotation? TryCreateKestrunAnnotation(
        AttributeAst attr,
        string defaultComponentName,
        string componentNameArgument)
    {
        var annotationType = ResolveKestrunAnnotationType(attr);
        if (annotationType is null)
        {
            return null;
        }

        if (Activator.CreateInstance(annotationType) is not KestrunAnnotation instance)
        {
            return null;
        }

        // Apply named arguments as property setters.
        foreach (var na in attr.NamedArguments ?? Enumerable.Empty<NamedAttributeArgumentAst>())
        {
            ApplyNamedArgument(instance, na);
        }

        // If the annotation supports a component-name argument, apply a default when not specified.
        // This preserves the previous behavior where the variable name becomes the component key.
        ApplyDefaultComponentName(instance, defaultComponentName, componentNameArgument);

        return instance;
    }

    /// <summary>
    /// Tries to evaluate an expression AST to a constant-like value.
    /// </summary>
    /// <param name="expr"> The expression AST to evaluate. </param>
    /// <returns>The constant-like value if evaluation is successful; otherwise, null.</returns>
    private static object? TryGetConstantLikeValue(ExpressionAst? expr)
    {
        if (expr is null)
        {
            return null;
        }

        // 1) Plain constants: 0, 123, $true, etc.
        if (expr is ConstantExpressionAst c)
        {
            return c.Value;
        }

        // 2) Plain strings: 'abc'
        if (expr is StringConstantExpressionAst s)
        {
            return s.Value;
        }

        // 3) [int]::MaxValue  (static member on a type)
        if (expr is MemberExpressionAst me && me.Static)
        {
            if (me.Expression is TypeExpressionAst te)
            {
                var targetType = te.TypeName.GetReflectionType(); // resolves [int] to System.Int32, etc.
                if (targetType is null)
                {
                    return null;
                }

                var memberName = (me.Member as StringConstantExpressionAst)?.Value
                                 ?? (me.Member as ConstantExpressionAst)?.Value?.ToString();

                if (string.IsNullOrWhiteSpace(memberName))
                {
                    return null;
                }

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

                // Try property first (MaxValue is a property on numeric types)
                var prop = targetType.GetProperty(memberName, flags);
                if (prop is not null)
                {
                    return prop.GetValue(null);
                }

                // Then field (covers other patterns)
                var field = targetType.GetField(memberName, flags);
                if (field is not null)
                {
                    return field.GetValue(null);
                }
            }
        }

        return null;
    }

    private static KestrunAnnotation? TryCmdletMetadataAttribute(
            AttributeAst attr)
    {
        var annotationType = ResolveCmdletMetadataAttributeType(attr);
        if (annotationType is null)
        {
            return null;
        }

        var instance = new InternalPowershellAttribute();
        switch (annotationType.Name) // <-- no GetType().Name
        {
            case nameof(ValidateRangeAttribute):
                {
                    var minObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(0));
                    var maxObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(1));

                    instance.MinRange = minObj?.ToString();
                    instance.MaxRange = maxObj?.ToString();
                    break;
                }

            case nameof(ValidateLengthAttribute):
                {
                    var minObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(0));
                    var maxObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(1));
                    if (int.TryParse(minObj?.ToString(), out var minLength))
                    {
                        instance.MinLength = minLength;
                    }

                    if (int.TryParse(maxObj?.ToString(), out var maxLength))
                    {
                        instance.MaxLength = maxLength;
                    }

                    break;
                }
            case nameof(ValidateSetAttribute):
                {
                    // PowerShell: [ValidateSet('a','b')] is positional arguments
                    instance.AllowedValues = [.. attr.PositionalArguments
                    .Select(a => TryGetConstantLikeValue(a)?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))];
                    break;
                }

            case nameof(ValidatePatternAttribute):
                {
                    // PowerShell: [ValidatePattern('regex')]
                    var patternObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(0));
                    instance.RegexPattern = patternObj?.ToString();
                    break;
                }

            case nameof(ValidateCountAttribute):
                {
                    // PowerShell: [ValidateCount(min, max)]
                    var minObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(0));
                    var maxObj = TryGetConstantLikeValue(attr.PositionalArguments.ElementAtOrDefault(1));

                    if (int.TryParse(minObj?.ToString(), out var minCount))
                    {
                        instance.MinItems = minCount;
                    }

                    if (int.TryParse(maxObj?.ToString(), out var maxCount))
                    {
                        instance.MaxItems = maxCount;
                    }

                    break;
                }

            case nameof(ValidateNotNullOrEmptyAttribute):
                instance.ValidateNotNullOrEmptyAttribute = true;
                break;

            case nameof(ValidateNotNullAttribute):
                instance.ValidateNotNullAttribute = true;
                break;

            case nameof(ValidateNotNullOrWhiteSpaceAttribute):
                instance.ValidateNotNullOrWhiteSpaceAttribute = true;
                break;
        }

        return instance;
    }
    private static void ApplyDefaultComponentName(Attribute annotation, string defaultComponentName, string componentNameArgument)
    {
        if (string.IsNullOrWhiteSpace(defaultComponentName))
        {
            return;
        }

        var t = annotation.GetType();
        var prop = t.GetProperty(componentNameArgument, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (prop is null || prop.PropertyType != typeof(string) || !prop.CanWrite)
        {
            return;
        }

        var current = prop.GetValue(annotation) as string;
        if (!string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        prop.SetValue(annotation, defaultComponentName);
    }

    private static void ApplyNamedArgument(KestrunAnnotation annotation, NamedAttributeArgumentAst na)
    {
        var t = annotation.GetType();
        var prop = t.GetProperty(na.ArgumentName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (prop is null || !prop.CanWrite)
        {
            return;
        }

        var raw = EvaluateArgumentExpression(na.Argument);
        var converted = ConvertToPropertyType(raw, prop.PropertyType);
        prop.SetValue(annotation, converted);
    }



    private static object? EvaluateArgumentExpression(ExpressionAst expr) => expr switch
    {
        StringConstantExpressionAst s => s.Value,
        ExpandableStringExpressionAst e => e.Value,
        ConstantExpressionAst c => c.Value,
        VariableExpressionAst v => EvaluateVariableExpression(v),
        MemberExpressionAst me => TryEvaluateMemberExpression(me) ?? me.Extent.Text.Trim(),
        TypeExpressionAst te => ResolveTypeFromName(te.TypeName.FullName) is { } t ? t : te.TypeName.FullName,
        _ => expr.Extent.Text.Trim()
    };

    private static object? EvaluateVariableExpression(VariableExpressionAst v)
    {
        var name = v.VariablePath.UserPath;
        if (string.Equals(name, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Unknown variable, preserve source text (e.g. $foo)
        return v.Extent.Text.Trim();
    }

    private static object? TryEvaluateMemberExpression(MemberExpressionAst me)
    {
        // Common pattern in scripts: [SomeType]::Member
        if (me.Expression is not TypeExpressionAst te)
        {
            return null;
        }

        var type = ResolveTypeFromName(te.TypeName.FullName);
        if (type is null)
        {
            return null;
        }

        var memberName = me.Member.Extent.Text.Trim().Trim('"', '\'');
        if (type.IsEnum)
        {
            try
            {
                return Enum.Parse(type, memberName, ignoreCase: true);
            }
            catch
            {
                return null;
            }
        }

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;
        var f = type.GetField(memberName, Flags);
        if (f is not null)
        {
            return f.GetValue(null);
        }
        var p = type.GetProperty(memberName, Flags);
        return p?.GetValue(null);
    }

    private static object? ConvertToPropertyType(object? raw, Type propertyType)
    {
        if (raw is null)
        {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (targetType.IsInstanceOfType(raw))
        {
            return raw;
        }

        // Enums
        if (targetType.IsEnum)
        {
            return raw is string s
                ? Enum.Parse(targetType, s, ignoreCase: true)
                : Enum.ToObject(targetType, raw);
        }

        // Booleans can show up as "$true" / "$false" when we fall back to Extent.Text
        if (targetType == typeof(bool) && raw is string bs)
        {
            if (string.Equals(bs, "$true", StringComparison.OrdinalIgnoreCase) || string.Equals(bs, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(bs, "$false", StringComparison.OrdinalIgnoreCase) || string.Equals(bs, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        try
        {
            return Convert.ChangeType(raw, targetType);
        }
        catch
        {
            // If we can't strongly convert, keep the raw value (often a string) rather than failing the scan.
            return raw;
        }
    }

    /// <summary>
    /// Resolves a KestrunAnnotation-derived type from an AttributeAst.
    /// </summary>
    /// <param name="attr">The AttributeAst to resolve the type from.</param>
    /// <returns>The resolved type if found; otherwise, null.</returns>
    private static Type? ResolveKestrunAnnotationType(AttributeAst attr)
    {
        // PowerShell attribute syntax allows omitting the 'Attribute' suffix.
        var shortName = attr.TypeName.Name;

        // If a namespace-qualified name is present, try it directly.
        var fullName = attr.TypeName.FullName;

        var type = ResolveTypeFromName(fullName);
        type ??= ResolveTypeFromName(shortName);
        if (type is null && !shortName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
        {
            type ??= ResolveTypeFromName(shortName + "Attribute");
        }

        return type is not null &&
            typeof(KestrunAnnotation).IsAssignableFrom(type) ? type : null;
    }

    /// <summary>
    /// Resolves a CmdletMetadataAttribute-derived type from an AttributeAst.
    /// </summary>
    /// <param name="attr">The AttributeAst to resolve the type from.</param>
    /// <returns>The resolved type if found; otherwise, null.</returns>
    private static Type? ResolveCmdletMetadataAttributeType(AttributeAst attr)
    {
        // PowerShell attribute syntax allows omitting the 'Attribute' suffix.
        var shortName = attr.TypeName.Name;

        // If a namespace-qualified name is present, try it directly.
        var fullName = attr.TypeName.FullName;

        var type = ResolveTypeFromName(fullName);
        type ??= ResolveTypeFromName(shortName);
        if (type is null && !shortName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
        {
            type ??= ResolveTypeFromName(shortName + "Attribute");
        }

        return type is not null &&
            typeof(CmdletMetadataAttribute).IsAssignableFrom(type)
             ? type : null;
    }

    private static Type? ResolveTypeFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Try Type.GetType first (works for assembly-qualified names)
        var t = Type.GetType(name, throwOnError: false, ignoreCase: true);
        if (t is not null)
        {
            return t;
        }

        // Search loaded assemblies (best-effort)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(name, throwOnError: false, ignoreCase: true);
            if (t is not null)
            {
                return t;
            }

            // Also allow matching by short type name.
            try
            {
                t = asm.GetTypes().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (t is not null)
                {
                    return t;
                }
            }
            catch
            {
                // Some dynamic/reflection-only assemblies can throw on GetTypes(). Ignore.
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if an attribute matches the provided filter.
    /// </summary>
    /// <param name="attr">The attribute to check.</param>
    /// <param name="filter">The filter of attribute type names to match against.</param>
    /// <returns>True if the attribute matches the filter; otherwise, false.</returns>
    private static bool IsMatchingAttribute(AttributeAst attr, IReadOnlyCollection<string>? filter)
    {
        if (filter is null || filter.Count == 0)
        {
            return true;
        }

        // Compare short type name: [ComponentParameter] or [Namespace.ComponentParameter]
        var shortName = attr.TypeName.Name;
        return filter.Any(x => string.Equals(x, shortName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tries to parse a standalone attribute line from a statement text.
    /// </summary>
    /// <param name="statementText">The text of the statement to parse for standalone attributes.</param>
    /// <returns>An array of AttributeAst instances if parsing is successful; otherwise, an empty array.</returns>
    private static IReadOnlyList<AttributeAst> TryParseStandaloneAttributeLine(string statementText)
    {
        var t = statementText.Trim();

        // Basic "looks like our DSL annotation"
        if (!t.StartsWith('[') ||
            !t.EndsWith(']') ||
            !t.Contains('(', StringComparison.Ordinal))
        {
            return [];
        }

        // Parse in a context where attributes are legal (param block)
        var synthetic = "param(\n" + t + "\n[object]$__x\n)\n";

        var ast = Parser.ParseInput(synthetic, out _, out var errors);
        if (errors is { Length: > 0 })
        {
            return [];
        }

        var paramBlock = ast.Find(n => n is ParamBlockAst, searchNestedScriptBlocks: true) as ParamBlockAst;
        var firstParam = paramBlock?.Parameters?.FirstOrDefault();
        return firstParam?.Attributes?
            .OfType<AttributeAst>()
            .ToArray()
        ?? [];
    }
}

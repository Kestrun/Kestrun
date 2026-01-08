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
    public sealed class AnnotatedVariable(string name)
    {
        /// <summary>Annotations attached to the variable.</summary>
        public List<KestrunAnnotation> Annotations { get; } = [];

        /// <summary>
        /// The variable name.
        /// </summary>
        public string Name { get; set; } = name;

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
            foreach (var resolved in ResolveDotSourcedFilesFromCommand(cmd, baseDir))
            {
                yield return resolved;
            }
        }
    }

    /// <summary>
    /// Resolves dot-sourced file paths from a PowerShell <see cref="CommandAst"/>.
    /// </summary>
    /// <param name="cmd">The command AST node to inspect.</param>
    /// <param name="baseDir">The base directory used to resolve relative paths.</param>
    /// <returns>Zero or more resolved dot-sourced file paths.</returns>
    private static IEnumerable<string?> ResolveDotSourcedFilesFromCommand(CommandAst cmd, string baseDir)
    {
        // Dot-sourcing in the PowerShell AST is represented via InvocationOperator = TokenKind.Dot,
        // and the dot token is NOT part of CommandElements.
        // Example: . "$PSScriptRoot\\a.ps1" => CommandElements[0] is the path expression.
        if (cmd.InvocationOperator == TokenKind.Dot)
        {
            if (TryResolveDotSourcedPathFromElements(cmd.CommandElements, elementIndex: 0, baseDir, out var resolved))
            {
                yield return resolved;
            }

            yield break;
        }

        // Back-compat / best-effort: some AST shapes could represent '.' as a command element.
        var elems = cmd.CommandElements;
        if (elems.Count < 2)
        {
            yield break;
        }

        if (!string.Equals(elems[0].Extent.Text.Trim(), ".", StringComparison.Ordinal))
        {
            yield break;
        }

        if (TryResolveDotSourcedPathFromElements(elems, elementIndex: 1, baseDir, out var resolvedCompat))
        {
            yield return resolvedCompat;
        }
    }

    /// <summary>
    /// Tries to resolve a dot-sourced file path from a specific command element.
    /// </summary>
    /// <param name="elements">The command elements to read from.</param>
    /// <param name="elementIndex">The element index expected to contain the dot-sourced path expression.</param>
    /// <param name="baseDir">The base directory used to resolve relative paths.</param>
    /// <param name="resolved">The resolved full file path, if available and exists.</param>
    /// <returns><c>true</c> if a file path was resolved and exists; otherwise <c>false</c>.</returns>
    private static bool TryResolveDotSourcedPathFromElements(IReadOnlyList<CommandElementAst> elements, int elementIndex, string baseDir, out string? resolved)
    {
        resolved = null;

        if (elements.Count <= elementIndex)
        {
            return false;
        }

        var raw = TryGetSimpleString(elements[elementIndex]);
        if (raw is null)
        {
            return false;
        }

        resolved = ResolveDotSourcedPath(raw, baseDir);
        return resolved is not null;
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

        // Normalize path separators to the platform-appropriate separator
        // This handles cases where PowerShell code uses backslashes on Unix-like systems
        t = t.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

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
        foreach (var block in GetNamedBlocks(scriptAst))
        {
            var statements = GetTopLevelStatements(block);

            var pending = new List<AttributeAst>();

            foreach (var st in statements)
            {
                if (TryHandleInlineAttributedAssignment(st, variables, attributeTypeFilter, componentNameArgument, pending))
                {
                    continue;
                }

                if (TryHandleInlineAttributedDeclaration(st, variables, attributeTypeFilter, componentNameArgument, pending))
                {
                    continue;
                }

                if (TryHandleStandaloneAttributeLine(st, attributeTypeFilter, pending))
                {
                    continue;
                }

                if (TryHandleVariableAssignment(st, variables, componentNameArgument, pending))
                {
                    continue;
                }

                if (TryHandleDeclarationOnlyVariable(st, variables, componentNameArgument, pending))
                {
                    continue;
                }

                // strict: anything else clears pending
                if (strict && pending.Count > 0)
                {
                    pending.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Gets the named blocks (<c>begin</c>, <c>process</c>, <c>end</c>) from a script AST.
    /// </summary>
    /// <param name="scriptAst">The script AST to inspect.</param>
    /// <returns>A list of non-null named blocks.</returns>
    private static List<Ast> GetNamedBlocks(ScriptBlockAst scriptAst)
    {
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

        return blocks;
    }

    /// <summary>
    /// Returns the top-level statements for a named block, ordered by lexical position.
    /// </summary>
    /// <param name="block">The named block AST node.</param>
    /// <returns>A list of statements in lexical order.</returns>
    private static List<StatementAst> GetTopLevelStatements(Ast block)
        => [.. block.FindAll(n => n is StatementAst sa && sa.Parent == block, searchNestedScriptBlocks: false)
            .Cast<StatementAst>()
            .OrderBy(s => s.Extent.StartOffset)];

    /// <summary>
    /// Handles inline attributed assignments, e.g. <c>[Attr()][int]$x = 1</c>, attaching annotations and defaults.
    /// </summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="variables">The variable map to populate.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    /// <param name="pending">The pending standalone attribute list (cleared on match).</param>
    /// <returns><c>true</c> if the statement was handled; otherwise <c>false</c>.</returns>
    private static bool TryHandleInlineAttributedAssignment(
        StatementAst statement,
        Dictionary<string, AnnotatedVariable> variables,
        IReadOnlyCollection<string>? attributeTypeFilter,
        string componentNameArgument,
        List<AttributeAst> pending)
    {
        if (statement is not AssignmentStatementAst inlineAssign)
        {
            return false;
        }

        if (!TryExtractInlineAttributedAssignment(inlineAssign, attributeTypeFilter, out var inlineVarName, out var inlineVarType, out var inlineVarTypeName, out var inlineAttrs))
        {
            return false;
        }

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
        return true;
    }

    /// <summary>
    /// Handles inline attributed declarations, e.g. <c>[Attr()][int]$x</c>, attaching annotations.
    /// </summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="variables">The variable map to populate.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    /// <param name="pending">The pending standalone attribute list (cleared on match).</param>
    /// <returns><c>true</c> if the statement was handled; otherwise <c>false</c>.</returns>
    private static bool TryHandleInlineAttributedDeclaration(
        StatementAst statement,
        Dictionary<string, AnnotatedVariable> variables,
        IReadOnlyCollection<string>? attributeTypeFilter,
        string componentNameArgument,
        List<AttributeAst> pending)
    {
        if (!TryExtractInlineAttributedDeclaration(statement, attributeTypeFilter, out var varName, out var varType, out var varTypeName, out var attrs))
        {
            return false;
        }

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
        return true;
    }

    /// <summary>
    /// Handles standalone attribute lines by accumulating matching attributes into <paramref name="pending"/>.
    /// </summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="pending">The pending standalone attribute list to append to.</param>
    /// <returns><c>true</c> if the statement was an attribute line; otherwise <c>false</c>.</returns>
    private static bool TryHandleStandaloneAttributeLine(
        StatementAst statement,
        IReadOnlyCollection<string>? attributeTypeFilter,
        List<AttributeAst> pending)
    {
        var parsedAttrs = TryParseStandaloneAttributeLine(statement.Extent.Text);
        if (parsedAttrs.Count == 0)
        {
            return false;
        }

        foreach (var a in parsedAttrs)
        {
            if (IsMatchingAttribute(a, attributeTypeFilter))
            {
                pending.Add(a);
            }
        }

        return true;
    }

    /// <summary>
    /// Handles variable assignments, attaching pending standalone attributes and capturing type/initializer.
    /// </summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="variables">The variable map to populate.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    /// <param name="pending">The pending standalone attribute list (cleared when applied).</param>
    /// <returns><c>true</c> if the statement was handled; otherwise <c>false</c>.</returns>
    private static bool TryHandleVariableAssignment(
        StatementAst statement,
        Dictionary<string, AnnotatedVariable> variables,
        string componentNameArgument,
        List<AttributeAst> pending)
    {
        if (statement is not AssignmentStatementAst assign)
        {
            return false;
        }

        if (!TryGetAssignmentTarget(assign.Left, out var targetVarName, out var targetVarType, out var targetVarTypeName))
        {
            return false;
        }

        var shouldCapture = pending.Count > 0 || variables.ContainsKey(targetVarName);
        if (!shouldCapture)
        {
            return false;
        }

        var (initValue, initExpr) = EvaluateValueStatement(assign.Right);
        var entry = GetOrCreateVariable(variables, targetVarName);

        ApplyVariableTypeInfo(entry, targetVarType, targetVarTypeName);
        ApplyInitializerValues(entry, initValue, initExpr);
        ApplyPendingAnnotations(entry, pending, targetVarName, componentNameArgument);

        pending.Clear();
        return true;
    }

    /// <summary>
    /// Applies variable type information to an AnnotatedVariable entry.
    /// </summary>
    /// <param name="entry">The entry to modify.</param>
    /// <param name="variableType">The variable type to apply.</param>
    /// <param name="variableTypeName">The variable type name to apply.</param>
    private static void ApplyVariableTypeInfo(AnnotatedVariable entry, Type? variableType, string? variableTypeName)
    {
        entry.VariableType ??= variableType;
        entry.VariableTypeName ??= variableTypeName;
    }

    /// <summary>
    /// Applies initializer values to an AnnotatedVariable entry, handling default/no-default cases.
    /// </summary>
    /// <param name="entry">The entry to modify.</param>
    /// <param name="initValue">The initial value to apply.</param>
    /// <param name="initExpr">The initial value expression to apply.</param>
    private static void ApplyInitializerValues(AnnotatedVariable entry, object? initValue, string? initExpr)
    {
        if (initExpr == "NoDefault")
        {
            entry.NoDefault = true;
            return;
        }

        entry.NoDefault = false;
        entry.InitialValue ??= initValue;
        entry.InitialValueExpression ??= initExpr;
    }

    /// <summary>
    /// Applies pending standalone attributes to an AnnotatedVariable entry.
    /// </summary>
    /// <param name="entry">The entry to modify.</param>
    /// <param name="pending">The pending attributes to apply.</param>
    /// <param name="targetVarName">The variable name used as default component name.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    private static void ApplyPendingAnnotations(
        AnnotatedVariable entry,
        List<AttributeAst> pending,
        string targetVarName,
        string componentNameArgument)
    {
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var a in pending)
        {
            var ka = TryCreateAnnotation(a, defaultComponentName: targetVarName, componentNameArgument);
            if (ka is not null)
            {
                entry.Annotations.Add(ka);
            }
        }
    }

    /// <summary>
    /// Handles declaration-only variables (no assignment), applying any pending standalone attributes.
    /// </summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="variables">The variable map to populate.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    /// <param name="pending">The pending standalone attribute list (cleared when applied).</param>
    /// <returns><c>true</c> if the statement was handled; otherwise <c>false</c>.</returns>
    private static bool TryHandleDeclarationOnlyVariable(
        StatementAst statement,
        Dictionary<string, AnnotatedVariable> variables,
        string componentNameArgument,
        List<AttributeAst> pending)
    {
        if (pending.Count == 0)
        {
            return false;
        }

        if (statement is not CommandExpressionAst declExpr)
        {
            return false;
        }

        if (!TryGetDeclaredVariableInfo(declExpr.Expression, out var declaredVarName, out var declaredVarType, out var declaredVarTypeName))
        {
            return false;
        }

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
        return true;
    }

    /// <summary>
    /// Gets or creates an AnnotatedVariable entry in the dictionary.
    /// </summary>
    /// <param name="variables"> The dictionary of variables to search or add to.</param>
    /// <param name="varName"> The name of the variable to get or create.</param>
    /// <returns>The existing or newly created AnnotatedVariable entry.</returns>
    private static AnnotatedVariable GetOrCreateVariable(Dictionary<string, AnnotatedVariable> variables, string varName)
    {
        if (!variables.TryGetValue(varName, out var entry))
        {
            entry = new AnnotatedVariable(varName);
            variables[varName] = entry;
        }

        return entry;
    }

    /// <summary>
    /// Tries to extract inline attributed variable declaration information from a statement AST.
    /// </summary>
    /// <param name="statement">The statement AST to inspect.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="variableName">Output variable name if found.</param>
    /// <param name="variableType">Output variable type if declared.</param>
    /// <param name="variableTypeName">Output variable type name as written in script if declared.</param>
    /// <param name="attributes">Output list of matching attributes found.</param>
    /// <returns><c>true</c> if matching attributes were found; otherwise <c>false</c>.</returns>
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

        if (!TryExtractExpressionFromStatement(statement, out var expr))
        {
            return false;
        }

        // Check for attributed-expression chain
        if (expr is null)
        {
            return false;
        }

        var found = CollectMatchingAttributesFromChain(expr, attributeTypeFilter);
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

    /// <summary>
    /// Tries to extract an expression from a statement AST, handling CommandExpression and Pipeline variants.
    /// </summary>
    /// <param name="statement">The statement AST to extract from.</param>
    /// <param name="expr">The extracted expression if found.</param>
    /// <returns><c>true</c> if an expression was successfully extracted; otherwise <c>false</c>.</returns>
    private static bool TryExtractExpressionFromStatement(StatementAst statement, out ExpressionAst? expr)
    {
        expr = statement switch
        {
            CommandExpressionAst ce => ce.Expression,
            PipelineAst p when p.PipelineElements is { Count: 1 } && p.PipelineElements[0] is CommandExpressionAst ce => ce.Expression,
            _ => null
        };

        return expr is not null;
    }

    /// <summary>
    /// Collects matching attributes from an attributed-expression chain.
    /// </summary>
    /// <param name="expr">The expression to traverse.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <returns>A list of matching attributes found in the chain.</returns>
    private static List<AttributeAst> CollectMatchingAttributesFromChain(ExpressionAst expr, IReadOnlyCollection<string>? attributeTypeFilter)
    {
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

        return found;
    }

    /// <summary>
    /// Tries to extract inline attributed variable assignment information from an assignment AST.
    /// </summary>
    /// <param name="assignment">The assignment statement AST to inspect.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="variableName">Output variable name if found.</param>
    /// <param name="variableType">Output variable type if declared.</param>
    /// <param name="variableTypeName">Output variable type name as written in script if declared.</param>
    /// <param name="attributes">Output list of matching attributes found.</param>
    /// <returns><c>true</c> if matching attributes were found; otherwise <c>false</c>.</returns>
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

    /// <summary>
    ///     Tries to extract the assignment target variable information from the left-hand side of an assignment.
    /// </summary>
    /// <param name="left">The left-hand side expression of the assignment.</param>
    /// <param name="variableName">Output variable name if found.</param>
    /// <param name="variableType">Output variable type if declared.</param>
    /// <param name="variableTypeName">Output variable type name as written in script if declared.</param>
    /// <returns><c>true</c> if a variable declaration was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Assignment can be: $x = 1   or   [int]$x = 1
    /// </remarks>
    private static bool TryGetAssignmentTarget(ExpressionAst left, out string variableName, out Type? variableType, out string? variableTypeName)
        => TryGetDeclaredVariableInfo(left, out variableName, out variableType, out variableTypeName);

    /// <summary>
    /// Tries to extract declared variable information from an expression AST.
    /// </summary>
    /// <param name="expr">The expression AST to inspect.</param>
    /// <param name="variableName">Output variable name if found.</param>
    /// <param name="variableType">Output variable type if declared.</param>
    /// <param name="variableTypeName">Output variable type name as written in script if declared.</param>
    /// <returns><c>true</c> if a variable declaration was found; otherwise <c>false</c>.</returns>
    private static bool TryGetDeclaredVariableInfo(ExpressionAst expr, out string variableName, out Type? variableType, out string? variableTypeName)
    {
        variableName = string.Empty;
        var cursor = expr;

        var attributedTypeNames = UnwrapAttributedExpressionChain(ref cursor);
        _ = TryUnwrapConvertExpression(ref cursor, out variableType, out variableTypeName);
        UnwrapRemainingAttributedExpressions(ref cursor);

        if (cursor is not VariableExpressionAst v)
        {
            return false;
        }

        variableName = v.VariablePath.UserPath;

        // PowerShell sometimes represents type constraints like [Nullable[datetime]]$x as another
        // attributed-expression rather than a ConvertExpressionAst. If we didn't get a type above,
        // try to infer it from the last non-annotation attribute type name.
        if (variableType is null && variableTypeName is null && attributedTypeNames.Count > 0 &&
            TryInferVariableTypeFromAttributes(attributedTypeNames, out var inferredType, out var inferredTypeName))
        {
            variableType = inferredType;
            variableTypeName = inferredTypeName;
        }

        return !string.IsNullOrWhiteSpace(variableName);
    }

    /// <summary>
    /// Unwraps an attributed-expression chain and collects any attribute type names encountered.
    /// </summary>
    /// <param name="cursor">The current expression cursor (updated to the innermost child).</param>
    /// <returns>A list of attribute type names encountered while unwrapping.</returns>
    private static List<string> UnwrapAttributedExpressionChain(ref ExpressionAst cursor)
    {
        var attributedTypeNames = new List<string>();

        while (cursor is AttributedExpressionAst aex)
        {
            if (!string.IsNullOrWhiteSpace(aex.Attribute?.TypeName?.FullName))
            {
                attributedTypeNames.Add(aex.Attribute.TypeName.FullName);
            }

            cursor = aex.Child;
        }

        return attributedTypeNames;
    }

    /// <summary>
    /// Unwraps a type conversion expression (e.g. <c>[int]$x</c>) when present and resolves the .NET type.
    /// </summary>
    /// <param name="cursor">The current expression cursor (updated to the conversion child when unwrapped).</param>
    /// <param name="variableType">The resolved .NET type for the conversion, if any.</param>
    /// <param name="variableTypeName">The declared type name as written in script, if any.</param>
    /// <returns><c>true</c> if a conversion expression was unwrapped; otherwise <c>false</c>.</returns>
    private static bool TryUnwrapConvertExpression(ref ExpressionAst cursor, out Type? variableType, out string? variableTypeName)
    {
        if (cursor is ConvertExpressionAst cex)
        {
            variableTypeName = cex.Type.TypeName.FullName;
            variableType = ResolvePowerShellTypeName(variableTypeName);
            cursor = cex.Child;
            return true;
        }

        variableType = null;
        variableTypeName = null;
        return false;
    }

    /// <summary>
    /// Unwraps any remaining attributed expressions (best-effort) after other unwrapping steps.
    /// </summary>
    /// <param name="cursor">The current expression cursor (updated to the innermost child).</param>
    private static void UnwrapRemainingAttributedExpressions(ref ExpressionAst cursor)
    {
        while (cursor is AttributedExpressionAst aex)
        {
            cursor = aex.Child;
        }
    }

    /// <summary>
    /// Tries to infer a declared variable type from the collected attribute type names, ignoring annotation attributes.
    /// </summary>
    /// <param name="attributedTypeNames">Attribute type names collected while unwrapping.</param>
    /// <param name="variableType">The inferred .NET type.</param>
    /// <param name="variableTypeName">The inferred type name as written in script.</param>
    /// <returns><c>true</c> if a non-annotation type was inferred; otherwise <c>false</c>.</returns>
    private static bool TryInferVariableTypeFromAttributes(IReadOnlyList<string> attributedTypeNames, out Type? variableType, out string? variableTypeName)
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

            variableType = t;
            variableTypeName = tn;
            return true;
        }

        variableType = null;
        variableTypeName = null;
        return false;
    }

    /// <summary>
    /// Resolves a PowerShell type name to a .NET Type, handling common accelerators and syntax.
    /// </summary>
    /// <param name="name">The PowerShell type name to resolve.</param>
    /// <returns>The resolved .NET Type, or null if resolution failed.</returns>
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

        if (TryGetPlainConstantValue(expr, out var value))
        {
            return value;
        }

        if (TryGetPlainStringValue(expr, out value))
        {
            return value;
        }

        if (TryGetStaticTypeMemberValue(expr, out value))
        {
            return value;
        }
        // Could extend with more expression types as needed.
        return null;
    }

    /// <summary>
    /// Tries to extract a plain constant value from an expression, such as numbers and booleans.
    /// </summary>
    /// <param name="expr">The expression to evaluate.</param>
    /// <param name="value">The extracted constant value.</param>
    /// <returns><c>true</c> if a constant value was extracted; otherwise <c>false</c>.</returns>
    private static bool TryGetPlainConstantValue(ExpressionAst expr, out object? value)
    {
        if (expr is ConstantExpressionAst c)
        {
            value = c.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to extract a plain string literal value from an expression.
    /// </summary>
    /// <param name="expr">The expression to evaluate.</param>
    /// <param name="value">The extracted string value.</param>
    /// <returns><c>true</c> if a string literal value was extracted; otherwise <c>false</c>.</returns>
    private static bool TryGetPlainStringValue(ExpressionAst expr, out object? value)
    {
        if (expr is StringConstantExpressionAst s)
        {
            value = s.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to evaluate a static type member expression (e.g. <c>[int]::MaxValue</c>) to its runtime value.
    /// </summary>
    /// <param name="expr">The expression to evaluate.</param>
    /// <param name="value">The evaluated member value.</param>
    /// <returns><c>true</c> if the expression was a supported static member and was evaluated; otherwise <c>false</c>.</returns>
    private static bool TryGetStaticTypeMemberValue(ExpressionAst expr, out object? value)
    {
        value = null;

        if (expr is not MemberExpressionAst me || !me.Static || me.Expression is not TypeExpressionAst te)
        {
            return false;
        }

        var targetType = te.TypeName.GetReflectionType(); // resolves [int] to System.Int32, etc.
        if (targetType is null)
        {
            return false;
        }

        var memberName = GetMemberName(me.Member);
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return false;
        }

        if (TryGetReflectedPropertyValue(targetType, memberName, out value))
        {
            return true;
        }

        if (TryGetReflectedFieldValue(targetType, memberName, out value))
        {
            return true;
        }

        // Unsupported member type
        return false;
    }

    /// <summary>
    /// Tries to retrieve and invoke a public static property from a type.
    /// </summary>
    /// <param name="targetType">The type to inspect.</param>
    /// <param name="memberName">The property name to retrieve.</param>
    /// <param name="value">The property value if found and invoked.</param>
    /// <returns><c>true</c> if the property was found and its value retrieved; otherwise <c>false</c>.</returns>
    private static bool TryGetReflectedPropertyValue(Type targetType, string memberName, out object? value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var prop = targetType.GetProperty(memberName, flags);

        if (prop is not null)
        {
            value = prop.GetValue(null);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to retrieve and invoke a public static field from a type.
    /// </summary>
    /// <param name="targetType">The type to inspect.</param>
    /// <param name="memberName">The field name to retrieve.</param>
    /// <param name="value">The field value if found and invoked.</param>
    /// <returns><c>true</c> if the field was found and its value retrieved; otherwise <c>false</c>.</returns>
    private static bool TryGetReflectedFieldValue(Type targetType, string memberName, out object? value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var field = targetType.GetField(memberName, flags);

        if (field is not null)
        {
            value = field.GetValue(null);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to extract a member name from a PowerShell member expression member node.
    /// </summary>
    /// <param name="member">The member expression node.</param>
    /// <returns>The member name if it can be extracted; otherwise, <c>null</c>.</returns>
    private static string? GetMemberName(CommandElementAst member)
        => (member as StringConstantExpressionAst)?.Value
           ?? (member as ConstantExpressionAst)?.Value?.ToString();

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

        return type is not null && typeof(KestrunAnnotation).IsAssignableFrom(type) ? type : null;
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

        return typeof(CmdletMetadataAttribute).IsAssignableFrom(type)
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

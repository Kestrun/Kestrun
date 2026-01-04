using System.Management.Automation;
using System.Management.Automation.Language;

namespace Kestrun.OpenApi;

/// <summary>
/// Represents a single attribute annotation extracted from PowerShell script source.
/// </summary>
/// <param name="TypeName">The full type name of the attribute, e.g. "Kestrun.OpenApi.ComponentParameterAttribute".</param>
/// <param name="PositionalArguments">The list of positional arguments provided to the attribute.</param>
/// <param name="NamedArguments">The dictionary of named arguments provided to the attribute.</param>
/// <param name="Source">The source location information for the attribute annotation.</param>
public sealed record AttributeInfo(
    string TypeName,
    IReadOnlyList<string> PositionalArguments,
    IReadOnlyDictionary<string, string> NamedArguments,
    SourceLocation Source
);

/// <summary>
/// Source location information for an attribute annotation.
/// </summary>
/// <param name="File">The file path where the attribute is located, or null if unknown.</param>
/// <param name="Line">The line number in the file where the attribute is located.</param>
/// <param name="Column">The column number in the line where the attribute starts.</param>
/// <param name="Text">The text content of the attribute annotation.</param>
public sealed record SourceLocation(
    string? File,
    int Line,
    int Column,
    string Text
);

/// <summary>
/// Scans PowerShell script files for OpenAPI component annotations defined via attributes.
/// </summary>
public static class OpenApiComponentAnnotationScanner
{
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
    /// <returns>A dictionary mapping component names to lists of extracted attribute annotations.</returns>
    /// <remarks>
    /// This method performs a breadth-first traversal of the script files starting from mainPath or $PSCommandPath,
    /// following dot-sourced includes and extracting attribute annotations that precede variable assignments.
    /// </remarks>
    public static Dictionary<string, List<AttributeInfo>> ScanFromRunningScriptOrPath(
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
    /// <returns>A dictionary mapping component names to lists of extracted attribute annotations.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the maximum number of files is exceeded while following dot-sourced scripts.</exception>
    /// <remarks>
    /// This method performs a breadth-first traversal of the script files starting from mainPath,
    /// following dot-sourced includes and extracting attribute annotations that precede variable assignments.
    /// </remarks>
    public static Dictionary<string, List<AttributeInfo>> ScanFromPath(
        string mainPath,
        IReadOnlyCollection<string>? attributeTypeFilter = null,
        string componentNameArgument = "Name",
        int maxFiles = 200)
    {
        mainPath = Path.GetFullPath(mainPath);

        var dict = new Dictionary<string, List<AttributeInfo>>(StringComparer.OrdinalIgnoreCase);

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

            ExtractAnnotationsFromAst(ast, dict, attributeTypeFilter, componentNameArgument);

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

        return dict;
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
    /// <param name="dict">The dictionary to populate with extracted annotations.</param>
    /// <param name="attributeTypeFilter">Optional filter for attribute types to include.</param>
    /// <param name="componentNameArgument">The argument name to use for component names.</param>
    /// <param name="strict">If true, clears pending annotations on non-matching statements.</param>
    private static void ExtractAnnotationsFromAst(
        ScriptBlockAst scriptAst,
        Dictionary<string, List<AttributeInfo>> dict,
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
            var statements = block.FindAll(n => n is StatementAst, searchNestedScriptBlocks: false)
                                  .Cast<StatementAst>()
                                  .OrderBy(s => s.Extent.StartOffset)
                                  .ToList();

            var pending = new List<AttributeAst>();

            foreach (var st in statements)
            {
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


                // Next variable assignment attaches pending annotations
                if (pending.Count > 0 &&
                    st is AssignmentStatementAst assign &&
                    assign.Left is VariableExpressionAst lhsVar)
                {
                    var varName = lhsVar.VariablePath.UserPath;

                    var componentName =
                        pending.Select(a => TryGetComponentName(a, componentNameArgument))
                               .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                        ?? varName;

                    if (!dict.TryGetValue(componentName, out var list))
                    {
                        list = [];
                        dict[componentName] = list;
                    }

                    foreach (var a in pending)
                    {
                        list.Add(ToInfo(a)); // recommend passing file in, since Extent.File is often null
                    }

                    pending.Clear();
                    continue;
                }
                // Declaration-only variable (no assignment), e.g. [int]$param1
                if (pending.Count > 0 && st is CommandExpressionAst declExpr)
                {
                    var declaredVar = TryGetDeclaredVariable(declExpr.Expression);
                    if (declaredVar is not null)
                    {
                        var varName = declaredVar.VariablePath.UserPath;

                        var componentName =
                            pending.Select(a => TryGetComponentName(a, componentNameArgument))
                                   .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                            ?? varName;

                        if (!dict.TryGetValue(componentName, out var list))
                        {
                            list = [];
                            dict[componentName] = list;
                        }

                        foreach (var a in pending)
                        {
                            list.Add(ToInfo(a));
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
    /// Tries to get the component name from the attribute arguments.
    /// </summary>
    /// <param name="attr">The attribute to extract the component name from.</param>
    /// <param name="componentNameArgument">The name of the argument that specifies the component name.</param>
    /// <returns>The component name if found; otherwise, null.</returns>
    private static string? TryGetComponentName(AttributeAst attr, string componentNameArgument)
    {
        // 1) Named argument: Name='limit'
        foreach (var na in attr.NamedArguments ?? Enumerable.Empty<NamedAttributeArgumentAst>())
        {
            if (string.Equals(na.ArgumentName, componentNameArgument, StringComparison.OrdinalIgnoreCase))
            {
                var raw = na.Argument.Extent.Text.Trim();
                return Unquote(raw);
            }
        }

        // 2) First positional: [ComponentParameter('limit')]
        if (attr.PositionalArguments is not null && attr.PositionalArguments.Count > 0)
        {
            var raw = attr.PositionalArguments[0].Extent.Text.Trim();
            return Unquote(raw);
        }

        return null;
    }

    /// <summary>
    /// Removes surrounding quotes from a string if present.
    /// </summary>
    /// <param name="raw">The string to unquote.</param>
    /// <returns>The unquoted string if quotes were present; otherwise, the original string.</returns>
    private static string Unquote(string raw)
    {
        if (raw.Length >= 2)
        {
            if ((raw[0] == '\'' && raw[^1] == '\'') || (raw[0] == '"' && raw[^1] == '"'))
            {
                return raw[1..^1];
            }
        }
        return raw;
    }

    private static AttributeInfo ToInfo(AttributeAst attr)
    {
        var positional = (attr.PositionalArguments ?? Enumerable.Empty<ExpressionAst>())
            .Select(a => a.Extent.Text.Trim())
            .ToList();

        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var na in attr.NamedArguments ?? Enumerable.Empty<NamedAttributeArgumentAst>())
        {
            named[na.ArgumentName] = na.Argument.Extent.Text.Trim();
        }

        var src = new SourceLocation(
            File: attr.Extent.File, // often null when parsing from text; you can set your own if you want
            Line: attr.Extent.StartLineNumber,
            Column: attr.Extent.StartColumnNumber,
            Text: attr.Extent.Text.Trim()
        );

        return new AttributeInfo(
            TypeName: attr.TypeName.FullName,
            PositionalArguments: positional,
            NamedArguments: named,
            Source: src
        );
    }

    /// <summary>
    /// Gets standalone attributes from an expression AST.
    /// </summary>
    /// <param name="expr">The expression AST to extract standalone attributes from.</param>
    /// <returns>An enumerable of standalone AttributeAst instances.</returns>
    /// <remarks>
    /// This method traverses the expression AST to find attributes that are not directly associated with a specific expression,
    /// effectively capturing attributes that stand alone and apply to subsequent statements.
    /// Handles:
    ///   [ComponentParameter(...)]
    ///   [A()][B()]   (nested AttributedExpressionAst)
    /// Ignores type constraints like [int] (TypeConstraintAst)
    /// </remarks>
    private static IEnumerable<AttributeAst> GetStandaloneAttributes(ExpressionAst expr)
    {

        while (expr is AttributedExpressionAst aex)
        {
            if (aex.Attribute is AttributeAst attr)
            {
                yield return attr;
            }

            expr = aex.Child;
        }
    }

    /// <summary>
    /// Tries to get the declared variable from an expression AST.
    /// </summary>
    /// <param name="expr">The expression AST to extract the declared variable from.</param>
    /// <returns>The declared VariableExpressionAst if found; otherwise, null.</returns>
    /// <remarks>
    /// This method traverses the expression AST to find a variable declaration,
    /// effectively capturing cases where a variable is declared without an assignment.
    /// Handles:
    ///   [int]$param1   (and similar type constraints)
    /// Returns the VariableExpressionAst if the final child is a variable.
    /// </remarks>
    private static VariableExpressionAst? TryGetDeclaredVariable(ExpressionAst expr)
    {
        // Matches: [int]$param1   (and similar type constraints)
        // Returns the VariableExpressionAst if the final child is a variable.
        while (expr is AttributedExpressionAst aex)
        {
            expr = aex.Child;
        }

        return expr as VariableExpressionAst;
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

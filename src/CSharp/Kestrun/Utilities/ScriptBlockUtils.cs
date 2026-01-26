using System.Text;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Kestrun.Utilities;

internal static class ScriptBlockUtils
{
    /// <summary>
    /// Returns only the executable statements from the scriptblock (Begin/Process/End),
    /// excluding attributes and the param(...) block.
    /// </summary>
    /// <param name="source">The source script block.</param>
    /// <returns>The script block body as text.</returns>
    public static string GetBodyText(ScriptBlock source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var fdAst = (FunctionDefinitionAst)source.Ast;
        var ast = fdAst.Body;
        var sb = new StringBuilder();

        static void Append(StringBuilder sb, NamedBlockAst? block)
        {
            if (block?.Statements == null || block.Statements.Count == 0)
            {
                return;
            }

            foreach (var stmt in block.Statements)
            {
                _ = sb.AppendLine(stmt.Extent.Text);
            }
        }

        Append(sb, ast.BeginBlock);
        Append(sb, ast.ProcessBlock);
        Append(sb, ast.EndBlock);

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Same as GetBodyText, but returns a new ScriptBlock created from the body text.
    /// </summary>
    /// <param name="source">The source script block.</param>
    /// <returns>The script block body as a ScriptBlock.</returns>
    public static ScriptBlock GetBodyScriptBlock(ScriptBlock source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var body = GetBodyText(source);
        return ScriptBlock.Create(body ?? string.Empty);
    }

    /// <summary>
    /// Returns the name of the first parameter in param(...), without the '$'.
    /// Example: param([KrFormData] $FormPayload) -> "FormPayload"
    /// Returns null if there is no param block or no parameters.
    /// </summary>
    /// <param name="source">The source script block.</param>
    /// <returns>The name of the first parameter, or null.</returns>
    public static string? GetFirstParameterName(ScriptBlock source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var ast = (ScriptBlockAst)source.Ast;
        var param0 = ast.ParamBlock?.Parameters?.FirstOrDefault();
        if (param0?.Name is null)
        {
            return null;
        }

        // VariablePath.UserPath is the clean name (no '$')
        return param0.Name.VariablePath.UserPath;
    }

    /// <summary>
    /// Returns the name of the first parameter typed as KrFormData or KrMultipart, without the '$'.
    /// </summary>
    /// <param name="source">The source script block.</param>
    /// <returns> The name of the first parameter typed as KrFormData or KrMultipart, without the '$'.</returns>
    public static string? GetFormPayloadParameterName(ScriptBlock source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var ast = (ScriptBlockAst)source.Ast;
        var parameters = ast.ParamBlock?.Parameters;
        if (parameters == null || parameters.Count == 0)
        {
            return null;
        }

        foreach (var p in parameters)
        {
            var type = p.StaticType;
            if (type == null)
            {
                continue;
            }

            // Works for qualified and unqualified types
            var name = type.Name;

            if (string.Equals(name, "KrFormData", StringComparison.Ordinal) ||
                string.Equals(name, "KrMultipart", StringComparison.Ordinal))
            {
                return p.Name.VariablePath.UserPath;
            }
        }

        return null;
    }
}

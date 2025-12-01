using System.Management.Automation;
using System.Management.Automation.Language;

namespace Kestrun.OpenApi;
/// <summary>
/// Helper to extract help information from PowerShell functions.
/// </summary>
public static class HelpExtractor
{
    /// <summary>
    /// Get the parsed comment-based help (CommentHelpInfo) for a PowerShell function
    /// using the AST + FunctionDefinitionAst.GetHelpContent().
    /// </summary>
    public static CommentHelpInfo? GetHelp(this FunctionInfo fn)
    {
        if (fn.ScriptBlock?.Ast is null)
        {
            return null;
        }

        // Case 1: the AST *is already* a FunctionDefinitionAst
        if (fn.ScriptBlock.Ast is FunctionDefinitionAst directFuncAst)
        {
            return directFuncAst.GetHelpContent();
        }

        // Case 2: the AST is a ScriptBlockAst (or something else),
        // and we need to search for the matching FunctionDefinitionAst
        if (fn.ScriptBlock.Ast is ScriptBlockAst scriptAst)
        {
            var funcAst = scriptAst.Find(
                ast => ast is FunctionDefinitionAst f &&
                       f.Name.Equals(fn.Name, StringComparison.OrdinalIgnoreCase),
                searchNestedScriptBlocks: true) as FunctionDefinitionAst;

            return funcAst?.GetHelpContent();
        }

        // Fallback: unknown AST shape
        return null;
    }

    /// <summary>
    /// Get the synopsis from the help information.
    /// </summary>
    /// <param name="help"> The help information to extract the synopsis from. </param>
    /// <returns>The synopsis text. </returns>
    public static string GetSynopsis(this CommentHelpInfo? help)
        => help?.Synopsis?.Trim() ?? string.Empty;

    /// <summary>
    /// Get the description from the help information.
    /// </summary>
    /// <param name="help"> The help information to extract the description from. </param>
    /// <returns>The description text.</returns>
    public static string GetDescription(this CommentHelpInfo? help)
        => help?.Description?.Trim() ?? string.Empty;

    /// <summary>
    /// Get the parameter description from the help information.
    /// </summary>
    /// <param name="help"> The help information to extract the parameter description from. </param>
    /// <param name="name"> The name of the parameter to get the description for. </param>
    /// <returns>The parameter description text, or null if not found.</returns>
    public static string? GetParameterDescription(this CommentHelpInfo? help, string name)
    {
        if (help?.Parameters == null || string.IsNullOrEmpty(name))
        {
            return null;
        }
        // Parameter names in CommentHelpInfo are stored in uppercase
        var key = name.ToUpperInvariant();
        // Try to get the parameter description
        return help.Parameters.TryGetValue(key, out var text)
            ? text?.Trim()
            : null;
    }
}

using System.Management.Automation;
using System.Management.Automation.Language;

namespace Kestrun.Utilities;

/// <summary>
/// Utilities for validating PowerShell ScriptBlocks.
/// </summary>
internal static class PsScriptBlockValidation
{
    /// <summary>
    /// Checks if the provided PowerShell script text contains only a param() block
    /// and nothing else (no executable statements).
    /// </summary>
    /// <param name="scriptText">The PowerShell script text to validate.</param>
    /// <param name="error">Output error message if validation fails.</param>
    /// <returns>True if the script contains only a param() block; otherwise, false.</returns>
    internal static bool HasParamAndNothingAfterParam_Ast(string scriptText, out string? error)
    {
        error = null;

        var ast = Parser.ParseInput(scriptText, out var tokens, out var errors);

        if (errors.Length > 0)
        {
            error = "Parse error: " + string.Join("; ", errors.Select(e => e.Message));
            return false;
        }

        if (ast is not ScriptBlockAst sbAst)
        {
            error = "Not a ScriptBlockAst.";
            return false;
        }

        // Find param block
        var paramAst = sbAst.ParamBlock;
        if (paramAst is null)
        {
            error = "No param() block found.";
            return false;
        }

        // Any executable statements in Begin/Process/End blocks?
        var hasAnyStatements =
            (sbAst.BeginBlock?.Statements?.Count ?? 0) > 0 ||
            (sbAst.ProcessBlock?.Statements?.Count ?? 0) > 0 ||
            (sbAst.EndBlock?.Statements?.Count ?? 0) > 0;

        if (hasAnyStatements)
        {
            error = "Statements found after param().";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the param() block is the last element in the ScriptBlock AST.
    /// </summary>
    /// <param name="sbAst">The ScriptBlock AST to check.</param>
    /// <returns>True if the param() block is the last element; otherwise, false.</returns>
    internal static bool IsParamLast(ScriptBlockAst sbAst)
    {
        if (sbAst.ParamBlock is null)
        {
            return false;
        }

        // Collect all executable statements in all blocks
        var allStatements =
            (sbAst.BeginBlock?.Statements ?? Enumerable.Empty<StatementAst>())
            .Concat(sbAst.ProcessBlock?.Statements ?? Enumerable.Empty<StatementAst>())
            .Concat(sbAst.EndBlock?.Statements ?? Enumerable.Empty<StatementAst>());

        // param() must be the last executable element, so no statements should exist
        return !allStatements.Any();
    }

    /// <summary>
    /// Checks if the provided PowerShell ScriptBlock has the param() block as the last element.
    /// </summary>
    /// <param name="scriptBlock">The PowerShell ScriptBlock to check.</param>
    /// <returns>True if the param() block is the last element; otherwise, false.</returns>
    internal static bool IsParamLast(ScriptBlock scriptBlock)
    {
        var ast = scriptBlock.Ast;
        return ast is FunctionDefinitionAst funcDefAst && IsParamLast(funcDefAst.Body);
    }
}

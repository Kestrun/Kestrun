using System.Management.Automation;
using System.Management.Automation.Language;

namespace Kestrun.Extensions;

/// <summary>
/// Extension methods for FunctionInfo.
/// </summary>
public static class FunctionInfoExtensions
{
    /// <summary>
    /// Gets the default value of a parameter in a FunctionInfo, if it has one.
    /// </summary>
    /// <param name="func">The FunctionInfo to inspect.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The default value of the parameter, or null if none exists.</returns>
    public static object? GetDefaultParameterValue(this FunctionInfo func, string paramName)
    {
        if (func.ScriptBlock?.Ast is not FunctionDefinitionAst fa)
        {
            return null;
        }
        if (fa.Body is not ScriptBlockAst scriptAst)
        {
            return null;
        }
        // Find the ParameterAst for the given parameter name
        var paramAst = scriptAst.ParamBlock?.Parameters
            .OfType<ParameterAst>()
            .FirstOrDefault(p =>
                p.Name?.VariablePath?.UserPath != null &&
                p.Name.VariablePath.UserPath.Equals(paramName, StringComparison.OrdinalIgnoreCase));

        if (paramAst?.DefaultValue is null)
        {
            return null; // no default
        }

        var defaultExpr = paramAst.DefaultValue;

        // Common cases: constant or array of constants
        return defaultExpr switch
        {
            ConstantExpressionAst c => c.Value,

            ArrayLiteralAst a when a.Elements.All(e => e is ConstantExpressionAst) =>
                a.Elements.Cast<ConstantExpressionAst>()
                    .Select(e => e.Value)
                    .ToArray(),

            // Fallback – return the textual expression if it’s something more complex
            _ => defaultExpr.Extent.Text
        };
    }
}

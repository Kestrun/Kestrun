using System.Text;

namespace Kestrun.OpenApi;

/// <summary>
/// Represents the result of mapping an RFC6570 path template to a Kestrel route pattern.
/// </summary>
/// <param name="OpenApiPattern">The OpenAPI path pattern (query expressions removed).</param>
/// <param name="KestrelPattern">The Kestrel route pattern with multi-segment parameters expanded.</param>
/// <param name="QueryParameters">Query parameter names extracted from RFC6570 query expressions.</param>
public sealed record Rfc6570PathMapping(
    string OpenApiPattern,
    string KestrelPattern,
    IReadOnlyList<string> QueryParameters);

/// <summary>
/// Maps OpenAPI 3.2 RFC6570 path templates to Kestrel-compatible route patterns.
/// </summary>
public static class Rfc6570PathTemplateMapper
{
    /// <summary>
    /// Attempts to map an RFC6570 path template to a Kestrel route pattern.
    /// </summary>
    /// <param name="openApiTemplate">The OpenAPI path template (RFC6570).</param>
    /// <param name="mapping">The resulting mapping for OpenAPI and Kestrel patterns.</param>
    /// <param name="error">The error message when mapping fails.</param>
    /// <returns>True when mapping succeeds; otherwise false.</returns>
    public static bool TryMapToKestrelRoute(
        string openApiTemplate,
        out Rfc6570PathMapping mapping,
        out string? error)
    {
        mapping = new Rfc6570PathMapping(string.Empty, string.Empty, []);
        error = null;

        if (string.IsNullOrWhiteSpace(openApiTemplate))
        {
            error = "OpenAPI path template is null or empty.";
            return false;
        }

        var openApiBuilder = new StringBuilder(openApiTemplate.Length);
        var kestrelBuilder = new StringBuilder(openApiTemplate.Length);
        var queryParameters = new List<string>();

        for (var i = 0; i < openApiTemplate.Length; i++)
        {
            var ch = openApiTemplate[i];
            if (ch != '{')
            {
                _ = openApiBuilder.Append(ch);
                _ = kestrelBuilder.Append(ch);
                continue;
            }

            var close = openApiTemplate.IndexOf('}', i + 1);
            if (close < 0)
            {
                error = "Unterminated RFC6570 expression: missing '}'.";
                return false;
            }

            var expression = openApiTemplate.Substring(i + 1, close - i - 1).Trim();
            if (expression.Length == 0)
            {
                error = "Empty RFC6570 expression '{}' is not supported.";
                return false;
            }

            if (!TryHandleExpression(expression, openApiBuilder, kestrelBuilder, queryParameters, out error))
            {
                return false;
            }

            i = close;
        }

        var openApiPattern = openApiBuilder.ToString();
        if (string.IsNullOrWhiteSpace(openApiPattern))
        {
            error = "OpenAPI path template resolved to an empty path.";
            return false;
        }

        mapping = new Rfc6570PathMapping(openApiPattern, kestrelBuilder.ToString(), queryParameters);
        return true;
    }

    /// <summary>
    /// Processes a single RFC6570 expression and appends to the OpenAPI and Kestrel builders.
    /// </summary>
    /// <param name="expression">The RFC6570 expression content (without braces).</param>
    /// <param name="openApiBuilder">Builder for OpenAPI path pattern.</param>
    /// <param name="kestrelBuilder">Builder for Kestrel route pattern.</param>
    /// <param name="queryParameters">List to collect query parameter names.</param>
    /// <param name="error">Error message when processing fails.</param>
    /// <returns>True on success; otherwise false.</returns>
    private static bool TryHandleExpression(
        string expression,
        StringBuilder openApiBuilder,
        StringBuilder kestrelBuilder,
        List<string> queryParameters,
        out string? error)
    {
        if (expression[0] == '#')
        {
            error = "RFC6570 fragment expressions ('#') are not supported in OpenAPI path templates.";
            return false;
        }

        if (expression[0] is '?' or '&')
        {
            var queryExpr = expression[1..];
            return TryParseQueryExpression(queryExpr, queryParameters, out error);
        }

        if (!TryParsePathExpression(expression, out var parsed, out error))
        {
            return false;
        }

        _ = openApiBuilder.Append(BuildOpenApiExpression(parsed));
        _ = kestrelBuilder.Append(BuildKestrelExpression(parsed));
        return true;
    }

    /// <summary>
    /// Parses a query expression (e.g. "id,filter") and appends parameter names.
    /// </summary>
    /// <param name="expression">The query expression content.</param>
    /// <param name="queryParameters">List to collect parameter names.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True on success; otherwise false.</returns>
    private static bool TryParseQueryExpression(
        string expression,
        List<string> queryParameters,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "RFC6570 query expression must include at least one variable.";
            return false;
        }

        foreach (var raw in expression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = raw.EndsWith('*') ? raw[..^1] : raw;
            if (!IsValidVarName(name))
            {
                error = $"Invalid RFC6570 variable name '{name}' in query expression.";
                return false;
            }
            queryParameters.Add(name);
        }

        return true;
    }

    /// <summary>
    /// Parses a path expression like {id}, {+path}, {path*}.
    /// </summary>
    /// <param name="expression">The expression content.</param>
    /// <param name="parsed">The parsed expression details.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True on success; otherwise false.</returns>
    private static bool TryParsePathExpression(string expression, out PathExpression parsed, out string? error)
    {
        parsed = new PathExpression(string.Empty, false, false);
        error = null;

        if (expression.Contains(':', StringComparison.Ordinal))
        {
            error = "Regex/constraint syntax (':') is not supported in OpenAPI 3.2 RFC6570 templates.";
            return false;
        }

        if (expression.Contains(',', StringComparison.Ordinal))
        {
            error = "Multiple variables in a single RFC6570 expression are not supported.";
            return false;
        }

        var isReserved = expression[0] == '+';
        if (isReserved)
        {
            expression = expression[1..];
            if (expression.Length == 0)
            {
                error = "Reserved RFC6570 expression '{+}' is not valid.";
                return false;
            }
        }

        var isExplode = expression.EndsWith('*');
        if (isExplode)
        {
            expression = expression[..^1];
            if (expression.Length == 0)
            {
                error = "Explode RFC6570 expression '{*}' is not valid.";
                return false;
            }
        }

        if (!IsValidVarName(expression))
        {
            error = $"Invalid RFC6570 variable name '{expression}'.";
            return false;
        }

        parsed = new PathExpression(expression, isReserved, isExplode);
        return true;
    }

    /// <summary>
    /// Builds the OpenAPI path expression for a parsed variable.
    /// </summary>
    /// <param name="parsed">The parsed expression details.</param>
    /// <returns>The RFC6570 expression for OpenAPI.</returns>
    private static string BuildOpenApiExpression(PathExpression parsed)
    {
        var builder = new StringBuilder();
        _ = builder.Append('{');
        if (parsed.IsReserved)
        {
            _ = builder.Append('+');
        }
        _ = builder.Append(parsed.Name);
        if (parsed.IsExplode)
        {
            _ = builder.Append('*');
        }
        _ = builder.Append('}');
        return builder.ToString();
    }

    /// <summary>
    /// Builds the Kestrel route expression for a parsed variable.
    /// </summary>
    /// <param name="parsed">The parsed expression details.</param>
    /// <returns>The Kestrel route expression.</returns>
    private static string BuildKestrelExpression(PathExpression parsed) => parsed.IsMultiSegment ? $"{{**{parsed.Name}}}" : $"{{{parsed.Name}}}";

    /// <summary>
    /// Validates RFC6570 variable names for the supported subset.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True if the name is valid; otherwise false.</returns>
    private static bool IsValidVarName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var c in name)
        {
            var ok = char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Represents a parsed RFC6570 path expression.
    /// </summary>
    /// <param name="Name">The variable name.</param>
    /// <param name="IsReserved">True when the expression is reserved expansion.</param>
    /// <param name="IsExplode">True when the expression uses explode modifier.</param>
    private sealed record PathExpression(string Name, bool IsReserved, bool IsExplode)
    {
        /// <summary>
        /// Gets a value indicating whether the expression represents a multi-segment variable.
        /// </summary>
        public bool IsMultiSegment => IsReserved || IsExplode;
    }
}

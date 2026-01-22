using System.Globalization;

namespace Kestrun.OpenApi;

/// <summary>
/// Maps ASP.NET Core route values to RFC 6570 URI Template variable assignments.
/// </summary>
public static partial class Rfc6570VariableMapper
{
    /// <summary>
    /// Attempts to build RFC 6570 variable assignments from an ASP.NET Core HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context containing route values from ASP.NET Core routing.</param>
    /// <param name="openApiPathTemplate">The OpenAPI 3.2 RFC 6570 path template (e.g., "/files/{+path}" or "/users/{id}").</param>
    /// <param name="variables">
    /// When this method returns true, contains a dictionary of variable names to their values.
    /// Variable names are extracted from the OpenAPI path template, and values are taken from ASP.NET route values.
    /// </param>
    /// <param name="error">When this method returns false, contains a human-readable error message.</param>
    /// <returns>
    /// True if variable assignments were successfully built; false if the context or template is invalid.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This helper only prepares variable values. Actual URI template expansion (percent-encoding rules)
    /// is performed by an RFC 6570 expander.
    /// </para>
    /// <list type="bullet">
    /// <item><description>Simple variables: {id}</description></item>
    /// <item><description>Reserved expansion: {+path} (multi-segment)</description></item>
    /// <item><description>Explode modifier: {path*} (multi-segment)</description></item>
    /// </list>
    /// <para>
    /// The OpenAPI template determines whether a variable is multi-segment ({+var} / {var*}).
    /// This method does not parse the ASP.NET route template to infer expansion semantics.
    /// </para>
    /// <example>
    /// <code>
    /// var template = "/users/{id}";
    /// if (Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var vars, out var error))
    /// {
    ///     // vars contains: { "id" = "123" }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public static bool TryBuildRfc6570Variables(
        HttpContext context,
        string openApiPathTemplate,
        out Dictionary<string, object> variables,
        out string? error)
    {
        variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (context is null)
        {
            error = "HttpContext is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(openApiPathTemplate))
        {
            error = "OpenAPI path template is null or empty.";
            return false;
        }

        var routeValues = context.Request.RouteValues;

        if (!TryParseTemplate(openApiPathTemplate, out var segments, out error))
        {
            return false;
        }

        foreach (var seg in segments)
        {
            if (!routeValues.TryGetValue(seg.Name, out var rawValue) || rawValue is null)
            {
                error = $"Missing required route value '{seg.Name}' for OpenAPI template '{openApiPathTemplate}'.";
                variables.Clear();
                return false;
            }

            if (!TryConvertRouteValueToInvariantString(rawValue, out var stringValue))
            {
                error = $"Route value '{seg.Name}' could not be converted to a string.";
                variables.Clear();
                return false;
            }

            if (seg.IsMultiSegment)
            {
                // Normalize: trim a single leading '/', preserve embedded '/'
                if (stringValue.Length > 0 && stringValue[0] == '/')
                {
                    stringValue = stringValue.TrimStart('/');
                }
            }
            else
            {
                if (stringValue.Contains('/', StringComparison.Ordinal))
                {
                    error = $"Route value '{seg.Name}' contains '/', but template '{seg.Name}' is not multi-segment.";
                    variables.Clear();
                    return false;
                }
            }

            variables[seg.Name] = stringValue;
        }

        return true;
    }

    /// <summary>
    /// Backwards-compatible overload that discards detailed error information.
    /// </summary>
    /// <param name="context">The HTTP context containing route values from ASP.NET Core routing.</param>
    /// <param name="openApiPathTemplate">The OpenAPI 3.2 RFC 6570 path template.</param>
    /// <param name="variables">When true, contains variable assignments.</param>
    /// <returns>True on success; otherwise false.</returns>
    public static bool TryBuildRfc6570Variables(
        HttpContext context,
        string openApiPathTemplate,
        out Dictionary<string, object?> variables)
    {
        var ok = TryBuildRfc6570Variables(context, openApiPathTemplate, out var vars, out _);
        variables = vars.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value, StringComparer.OrdinalIgnoreCase);
        return ok;
    }

    /// <summary>
    /// Represents a parsed RFC 6570 variable segment from an OpenAPI 3.2 path template.
    /// </summary>
    private sealed record VarSegment(string Name, bool IsReservedExpansion, bool IsExplode)
    {
        /// <summary>
        /// True when the variable can represent multiple path segments.
        /// </summary>
        public bool IsMultiSegment => IsReservedExpansion || IsExplode;
    }

    /// <summary>
    /// Parses an OpenAPI 3.2 RFC 6570 path template using a restricted subset:
    /// {var}, {+var}, {var*}, {+var*}.
    /// </summary>
    /// <param name="template">The OpenAPI path template.</param>
    /// <param name="segments">Parsed variable segments.</param>
    /// <param name="error">Error details on failure.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    private static bool TryParseTemplate(string template, out List<VarSegment> segments, out string? error)
    {
        segments = [];
        error = null;

        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
            {
                continue;
            }

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                error = "Unterminated RFC6570 expression: missing '}'.";
                return false;
            }

            var inner = template.Substring(i + 1, close - i - 1).Trim();
            if (inner.Length == 0)
            {
                error = "Empty RFC6570 expression '{}' is not supported.";
                return false;
            }

            if (inner.Contains(':', StringComparison.Ordinal))
            {
                error = "Regex/constraint syntax (':') is not supported in OpenAPI 3.2 RFC6570 path templates.";
                return false;
            }

            if (inner.Contains(',', StringComparison.Ordinal))
            {
                error = "Multiple variables in a single RFC6570 expression (e.g. '{a,b}') are not supported.";
                return false;
            }

            var isReserved = inner[0] == '+';
            if (isReserved)
            {
                inner = inner[1..];
                if (inner.Length == 0)
                {
                    error = "Reserved RFC6570 expression '{+}' is not valid.";
                    return false;
                }
            }

            var isExplode = inner.EndsWith('*');
            if (isExplode)
            {
                inner = inner[..^1];
                if (inner.Length == 0)
                {
                    error = "Explode RFC6570 expression '{*}' is not valid.";
                    return false;
                }
            }

            if (!IsValidVarName(inner))
            {
                error = $"Invalid RFC6570 variable name '{inner}'.";
                return false;
            }

            segments.Add(new VarSegment(inner, isReserved, isExplode));
            i = close;
        }

        return true;
    }

    /// <summary>
    /// Converts a route value to an invariant string representation.
    /// </summary>
    /// <param name="value">The route value.</param>
    /// <param name="stringValue">Invariant string representation.</param>
    /// <returns>True if conversion succeeded; otherwise false.</returns>
    private static bool TryConvertRouteValueToInvariantString(object value, out string stringValue)
    {
        // Route values commonly come in as string, int, long, Guid, etc.
        if (value is string s)
        {
            stringValue = s;
            return true;
        }

        if (value is IFormattable f)
        {
            stringValue = f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }

        stringValue = value.ToString() ?? string.Empty;
        return true;
    }

    /// <summary>
    /// Validates RFC6570 variable names for the supported subset.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <returns>True if name is acceptable; otherwise false.</returns>
    private static bool IsValidVarName(string name)
    {
        // Conservative: allow letters/digits/_ and .-
        // (PowerShell and ASP.NET route values tend to be simple identifiers.)
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var ok = char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}

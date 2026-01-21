using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

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
    /// <param name="openApiPathTemplate">The OpenAPI 3.2 path template (e.g., "/files/{+path}" or "/users/{userId:[0-9]+}").</param>
    /// <param name="variables">
    /// When this method returns true, contains a dictionary of variable names to their values.
    /// Variable names are extracted from the OpenAPI path template, and values are taken from ASP.NET route values.
    /// </param>
    /// <returns>
    /// True if variable assignments were successfully built; false if the context or template is invalid.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method supports OpenAPI 3.2 path expressions with RFC 6570 URI Template syntax:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Simple parameters: {id} maps to route value "id"</description></item>
    /// <item><description>Reserved operator: {+path} maps to route value "path" (multi-segment)</description></item>
    /// <item><description>Regex constraints: {userId:[0-9]+} maps to route value "userId"</description></item>
    /// </list>
    /// <para>
    /// The method extracts parameter names from the OpenAPI template and looks them up in the
    /// ASP.NET Core route values (HttpContext.Request.RouteValues).
    /// </para>
    /// <example>
    /// <code>
    /// // Given an ASP.NET route that matched "/api/v1/users/42"
    /// // with route values: { "version" = "1", "userId" = "42" }
    /// 
    /// var template = "/api/v{version:[0-9]+}/users/{userId}";
    /// if (Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var vars))
    /// {
    ///     // vars contains: { "version" = "1", "userId" = "42" }
    ///     // These can be used to expand RFC 6570 templates in links, callbacks, etc.
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public static bool TryBuildRfc6570Variables(
        HttpContext context,
        string openApiPathTemplate,
        out Dictionary<string, object?> variables)
    {
        variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (context is null || string.IsNullOrWhiteSpace(openApiPathTemplate))
        {
            return false;
        }

        var routeValues = context.Request.RouteValues;

        // Extract parameter names from the OpenAPI template
        var parameterNames = ExtractTemplateParameterNames(openApiPathTemplate);

        foreach (var paramName in parameterNames)
        {
            // Look up the parameter in route values
            if (routeValues is not null && routeValues.TryGetValue(paramName, out var value))
            {
                variables[paramName] = value;
            }
            else
            {
                // Parameter not found in route values - use null
                variables[paramName] = null;
            }
        }

        return true;
    }

    /// <summary>
    /// Extracts parameter names from an OpenAPI 3.2 path template.
    /// </summary>
    /// <param name="template">
    /// The OpenAPI path template (e.g., "/files/{+path}" or "/users/{userId:[0-9]+}").
    /// </param>
    /// <returns>A set of parameter names found in the template.</returns>
    /// <remarks>
    /// Supports RFC 6570 reserved operator (+) and regex constraints with colon notation.
    /// Regex constraints can contain braces (e.g., {itemId:[0-9a-f]{8}}).
    /// Examples:
    /// <list type="bullet">
    /// <item><description>{id} → "id"</description></item>
    /// <item><description>{+path} → "path"</description></item>
    /// <item><description>{userId:[0-9]+} → "userId"</description></item>
    /// <item><description>{itemId:[0-9a-f]{8}} → "itemId"</description></item>
    /// </list>
    /// </remarks>
    private static HashSet<string> ExtractTemplateParameterNames(string template)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(template))
        {
            return names;
        }

        // Match RFC 6570 style parameters: {id}, {+path}, {userId:[0-9]+}, {itemId:[0-9a-f]{8}}
        // Pattern breakdown:
        // \{           - Opening brace
        // \+?          - Optional '+' reserved operator
        // (?<name>     - Named capture group for parameter name
        //   [^{}:/\?]+ - One or more chars that are NOT: { } : / ?
        // )
        // (?:[:][^\}]+)? - Optional non-capturing group for regex constraint (everything after : until })
        // \}           - Closing brace
        var regex = Rfc6570ParameterRegex();
        
        foreach (Match match in regex.Matches(template))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Compiled regex for matching RFC 6570 URI Template parameters.
    /// Matches: {id}, {+path}, {userId:[0-9]+}, {itemId:[0-9a-f]{8}}
    /// Pattern matches parameters with optional reserved operator (+) and optional regex constraints.
    /// The regex constraint can contain anything after the colon until the closing brace.
    /// </summary>
    [GeneratedRegex(@"\{\+?(?<name>[^{}:/\?]+)(?:[:][^\}]+)?\}", RegexOptions.Compiled)]
    private static partial Regex Rfc6570ParameterRegex();
}

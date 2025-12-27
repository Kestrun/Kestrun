using System.Text.RegularExpressions;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Utility class to generate operation IDs for OpenAPI callbacks.
/// </summary>
internal static partial class CallbackOperationId
{
    private static readonly Regex NonIdChars = MyRegex();
    private static readonly Regex MultiUnderscore = MyRegex1();

    /// <summary>
    /// Generates a standardized operation ID for an OpenAPI callback based on the callback name, HTTP verb, and route pattern.
    /// </summary>
    /// <param name="callbackName">The name of the callback.</param>
    /// <param name="httpVerb">The HTTP verb associated with the callback.</param>
    /// <param name="pattern">The route pattern for the callback.</param>
    /// <returns>A standardized operation ID string.</returns>
    /// <exception cref="ArgumentException">Thrown when any of the input parameters are null or whitespace.</exception>
    internal static string From(string callbackName, string httpVerb, string pattern)
    {
        if (string.IsNullOrWhiteSpace(callbackName))
        {
            throw new ArgumentException("callbackName is required", nameof(callbackName));
        }

        if (string.IsNullOrWhiteSpace(httpVerb))
        {
            throw new ArgumentException("httpVerb is required", nameof(httpVerb));
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("pattern is required", nameof(pattern));
        }

        var verb = httpVerb.Trim().ToLowerInvariant();

        // Example: "/order/status/{orderId}" -> "order_status_orderId"
        var suffix = pattern.Trim();
        suffix = suffix.StartsWith("/") ? suffix[1..] : suffix;
        suffix = suffix.Replace("/", "_")
                       .Replace("{", "")
                       .Replace("}", "");

        suffix = NonIdChars.Replace(suffix, "_");
        suffix = MultiUnderscore.Replace(suffix, "_").Trim('_');

        // If you want *exactly* "...__status" for "/order/status/{orderId}",
        // you can optionally take a specific segment (see helper below).
        return $"{callbackName}__{verb}__{suffix}";
    }

    /// <summary>
    /// Generates a standardized operation ID for an OpenAPI callback based on the callback name, HTTP verb, and route pattern,
    /// using only the last non-parameter segment of the route pattern.
    /// </summary>
    /// <param name="callbackName">The name of the callback.</param>
    /// <param name="httpVerb">The HTTP verb associated with the callback.</param>
    /// <param name="pattern">The route pattern for the callback.</param>
    /// <returns>A standardized operation ID string.</returns>
    internal static string FromLastSegment(string callbackName, string httpVerb, string pattern)
    {
        if (string.IsNullOrWhiteSpace(callbackName))
        {
            throw new ArgumentException("callbackName is required", nameof(callbackName));
        }

        if (string.IsNullOrWhiteSpace(httpVerb))
        {
            throw new ArgumentException("httpVerb is required", nameof(httpVerb));
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("pattern is required", nameof(pattern));
        }

        var verb = httpVerb.Trim().ToLowerInvariant();
        var parts = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        var last = "op";
        if (parts.Length > 0)
        {
            last = parts[^1];
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                if (!parts[i].StartsWith('{'))
                {
                    last = parts[i];
                    break;
                }
            }
        }

        last = last.Trim('{', '}');
        last = NonIdChars.Replace(last, "_");
        last = MultiUnderscore.Replace(last, "_").Trim('_');

        return $"{callbackName}__{verb}__{last}";
    }

    /// <summary>
    /// Builds the callback key from the runtime expression and the pattern.
    /// </summary>
    /// <param name="expression">The runtime expression for the callback.</param>
    /// <param name="pattern">The route pattern for the callback.</param>
    /// <returns>A combined callback key as a RuntimeExpression.</returns>
    /// <exception cref="ArgumentException">Thrown when any of the input parameters are null or whitespace.</exception>
    internal static RuntimeExpression BuildCallbackKey(string expression, string pattern)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("expression required", nameof(expression));
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("pattern required", nameof(pattern));
        }

        expression = expression.Trim();
        pattern = pattern.Trim();

        // Normalize expression into braced form: {$request...}
        if (expression.StartsWith('{'))
        {
            // If user gave "{...}", ensure the inner begins with '$'
            // Example: "{request.body#/x}" -> "{$request.body#/x}"
            if (expression.Length >= 2 && expression[1] != '$')
            {
                expression = "{$" + expression[1..];
            }
        }
        else
        {
            // If user gave "$request..." or "request...", normalize to "$request..."
            if (!expression.StartsWith('$'))
            {
                expression = "$" + expression;
            }

            expression = "{" + expression + "}";
        }

        // Ensure pattern starts with '/'
        if (!pattern.StartsWith('/'))
        {
            pattern = "/" + pattern;
        }

        // If expression ends with '/', avoid "//"
        if (expression.EndsWith('/') && pattern.Length > 0 && pattern[0] == '/')
        {
            pattern = pattern[1..];
        }

        return RuntimeExpression.Build(expression + pattern);
    }

    [GeneratedRegex(@"[^A-Za-z0-9_]+", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
    [GeneratedRegex(@"_+", RegexOptions.Compiled)]
    private static partial Regex MyRegex1();
}

using System.Collections;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Factory helpers to build OpenApiLink from PowerShell-friendly inputs.
/// </summary>
public static class OpenApiLinkFactory
{
    /// <summary>
    /// Create an OpenApiLink using strings/hashtables similar to the PowerShell helper.
    /// </summary>
    public static OpenApiLink Create(
        string? operationRef,
        string? operationId,
        string? description,
        OpenApiServer? server,
        IDictionary? parameters,
        object? requestBody)
    {
        var link = new OpenApiLink();

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            link.OperationId = operationId;
        }
        else if (!string.IsNullOrWhiteSpace(operationRef))
        {
            link.OperationRef = operationRef;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            link.Description = description;
        }

        if (server is not null)
        {
            link.Server = server;
        }

        if (parameters is not null)
        {
            var dict = new Dictionary<string, RuntimeExpressionAnyWrapper>(StringComparer.Ordinal);
            foreach (DictionaryEntry de in parameters)
            {
                var key = de.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key)) { continue; }
                var wrapper = BuildWrapper(de.Value);
                dict[key!] = wrapper;
            }
            link.Parameters = dict;
        }

        if (requestBody is not null)
        {
            link.RequestBody = BuildWrapper(requestBody);
        }

        return link;

        static RuntimeExpressionAnyWrapper BuildWrapper(object? value)
        {
            var w = new RuntimeExpressionAnyWrapper();
            if (value is string s && s.TrimStart().StartsWith("$", StringComparison.Ordinal))
            {
                // Prefer the official builder
                w.Expression = RuntimeExpression.Build(s);
                return w;
            }

            // Literal: set Any based on expected property type (JsonNode or IOpenApiAny)
            var anyProp = typeof(RuntimeExpressionAnyWrapper).GetProperty("Any");
            if (anyProp is not null)
            {
                var pType = anyProp.PropertyType;
                if (pType.FullName == "System.Text.Json.Nodes.JsonNode")
                {
                    var node = OpenApiJsonNodeFactory.FromObject(value);
                    anyProp.SetValue(w, node);
                }
                else
                {
                    var any = OpenApiAnyFactory.FromObject(value);
                    anyProp.SetValue(w, any);
                }
            }
            return w;
        }
    }
}

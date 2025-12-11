#if EXTENDED_OPENAPI
using System.Reflection;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{

    #region Links
    /// <summary>
    /// Builds link components from the specified type.
    /// </summary>
    /// <param name="t">The type to build links for.</param>
    private void BuildLinks(Type t)
    {
        string? defaultDescription = null;
        string? joinClassName = null;
        // Ensure Links dictionary exists
        Document.Components!.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        // ------ Build Links -------
        foreach (var p in t.GetProperties(flags))
        {
            var link = new OpenApiLink();

            var classAttrs = t.GetCustomAttributes(inherit: false).
                               Where(a => a.GetType().Name is
                               nameof(OpenApiLinkComponent))
                               .Cast<object>()
                               .ToArray();
            if (classAttrs.Length > 0)
            {
                // Apply any class-level [OpenApiLinkComponent] attributes first
                if (classAttrs[0] is OpenApiLinkComponent classLinkAttr)
                {
                    if (!string.IsNullOrEmpty(classLinkAttr.Description))
                    {
                        defaultDescription = classLinkAttr.Description;
                    }
                    if (!string.IsNullOrEmpty(classLinkAttr.JoinClassName))
                    {
                        joinClassName = t.FullName + classLinkAttr.JoinClassName;
                    }
                }
            }

            var attrs = p.GetCustomAttributes(inherit: false)
                                  .Where(a => a is OpenApiLinkAttribute or
                                    OpenApiServerAttribute or
                                    OpenApiServerVariableAttribute)
                                  .Cast<object>()
                                  .ToArray();

            if (attrs.Length == 0) { continue; }
            var customName = string.Empty;
            foreach (var a in attrs)
            {
                if (a is OpenApiLinkAttribute oaHa)
                {
                    if (!string.IsNullOrWhiteSpace(oaHa.Key))
                    {
                        customName = oaHa.Key;
                    }
                }
                _ = CreateLinkFromAttribute(a, link);
            }
            var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName!;
            var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;
            if (link.Description is null && defaultDescription is not null)
            {
                link.Description = defaultDescription;
            }
            Document.Components!.Links![key] = link;
        }
    }

    /// <summary>
    /// Creates an OpenApiLink from the specified attribute.
    /// </summary>
    /// <param name="attr">The attribute to create the link from.</param>
    /// <param name="link">The link to populate.</param>
    /// <returns>True if the link was successfully created; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the attribute is invalid.</exception>
    private static bool CreateLinkFromAttribute(object attr, OpenApiLink link)
    {
        if (attr is OpenApiLinkAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.OperationId) &&
               !string.IsNullOrWhiteSpace(attribute.OperationRef))
            {
                throw new InvalidOperationException("OpenApiLinkAttribute cannot have both OperationId and OperationRef specified.");
            }
            if (!string.IsNullOrWhiteSpace(attribute.RequestBodyExpression) &&
                !string.IsNullOrWhiteSpace(attribute.RequestBodyJson))
            {
                throw new InvalidOperationException("OpenApiLinkAttribute cannot have both RequestBodyExpression and RequestBodyJson specified.");
            }
            // Populate link fields
            if (!string.IsNullOrWhiteSpace(attribute.Description))
            {
                link.Description = attribute.Description;
            }
            if (!string.IsNullOrWhiteSpace(attribute.OperationId))
            {
                if (link.OperationRef is not null)
                {
                    throw new InvalidOperationException("OpenApiLink cannot have both OperationId and OperationRef specified.");
                }
                link.OperationId = attribute.OperationId;
            }
            if (!string.IsNullOrWhiteSpace(attribute.OperationRef))
            {
                if (link.OperationId is not null)
                {
                    throw new InvalidOperationException("OpenApiLink cannot have both OperationId and OperationRef specified.");
                }
                link.OperationRef = attribute.OperationRef;
            }
            if (!string.IsNullOrWhiteSpace(attribute.MapKey) && !string.IsNullOrWhiteSpace(attribute.MapValue))
            {
                link.Parameters ??= new Dictionary<string, RuntimeExpressionAnyWrapper>(StringComparer.Ordinal);

                link.Parameters[attribute.MapKey] = new RuntimeExpressionAnyWrapper()
                {
                    Expression = RuntimeExpression.Build(attribute.MapValue)
                };
            }
            if (!string.IsNullOrWhiteSpace(attribute.RequestBodyExpression))
            {
                link.RequestBody = new RuntimeExpressionAnyWrapper()
                {
                    Expression = RuntimeExpression.Build(attribute.RequestBodyExpression)
                };
            }
            if (!string.IsNullOrWhiteSpace(attribute.RequestBodyJson))
            {
                link.RequestBody = new RuntimeExpressionAnyWrapper()
                {
                    Any = ToNode(attribute.RequestBodyJson)
                };
            }
            return true;
        }
        else if (attr is OpenApiServerAttribute server)
        {
            link.Server ??= new OpenApiServer();
            if (!string.IsNullOrWhiteSpace(server.Description))
            {
                link.Server.Description = server.Description;
            }
            link.Server.Url = server.Url;
            return true;
        }
        else if (attr is OpenApiServerVariableAttribute serverVariable)
        {
            if (string.IsNullOrWhiteSpace(serverVariable.Name))
            {
                throw new InvalidOperationException("OpenApiServerVariableAttribute requires a non-empty Name property.");
            }
            link.Server ??= new OpenApiServer();
            link.Server.Variables ??= new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal);
            var osv = new OpenApiServerVariable();
            if (!string.IsNullOrWhiteSpace(serverVariable.Default))
            {
                osv.Default = serverVariable.Default;
            }
            if (!string.IsNullOrWhiteSpace(serverVariable.Description))
            {
                osv.Description = serverVariable.Description;
            }
            if (serverVariable.Enum is not null && serverVariable.Enum.Length > 0)
            {
                osv.Enum = [.. serverVariable.Enum];
            }
            link.Server.Variables[serverVariable.Name] = osv;
            return true;
        }
        return false;
    }

    #endregion
    #region Callbacks
    private void BuildCallbacks(Type t)
    {
        Document.Components!.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);

        // Instantiate the class to pull default values
        object? inst = null;
        try { inst = Activator.CreateInstance(t); } catch { }

        string? description = null;
        var expressions = new List<string>();
        OpenApiPathItem? providedPathItem = null;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in t.GetProperties(flags))
        {
            object? val = null;
            try { val = inst is not null ? p.GetValue(inst) : null; } catch { }

            switch (p.Name)
            {
                case "Description":
                    description = val as string; break;
                case "Expression":
                    if (val is string s && !string.IsNullOrWhiteSpace(s)) { expressions.Add(s); }
                    break;
                case "Expressions":
                    if (val is System.Collections.IEnumerable en)
                    {
                        foreach (var item in en)
                        {
                            if (item is string es && !string.IsNullOrWhiteSpace(es)) { expressions.Add(es); }
                        }
                    }
                    break;
                case "PathItem":
                    if (val is OpenApiPathItem pi) { providedPathItem = pi; }
                    break;
                default:
                    break;
            }
        }

        if (expressions.Count == 0)
        {
            // Nothing to build
            return;
        }

        var cb = new OpenApiCallback();
        // Build a minimal PathItem if one was not provided
        var pathItem = providedPathItem ?? new OpenApiPathItem { Description = description };

        // Resolve the exact IDictionary<string, OpenApiPathItem> interface and its Add method
        var dictIface = typeof(OpenApiCallback)
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType
                               && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                               && i.GetGenericArguments()[0] == typeof(string)
                               && i.GetGenericArguments()[1] == typeof(OpenApiPathItem));
        var addMethod = dictIface?.GetMethod("Add", [typeof(string), typeof(OpenApiPathItem)]);
        foreach (var expr in expressions.Distinct(StringComparer.Ordinal))
        {
            if (addMethod is not null)
            {
                _ = addMethod.Invoke(cb, [expr, pathItem]);
            }
            else
            {
                // Fallback: try IDictionary<string, object> add via dynamic as a last resort
                try { ((dynamic)cb).Add(expr, pathItem); } catch { /* ignore */ }
            }
        }

        Document.Components!.Callbacks[t.Name] = cb;
    }
    #endregion
}
#endif


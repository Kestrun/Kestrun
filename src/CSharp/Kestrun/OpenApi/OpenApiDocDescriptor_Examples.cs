using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds example components from the specified type.
    /// </summary>
    /// <param name="t">The type to build examples from.</param>
    private void BuildExamples(Type t)
    {
        // Ensure Examples dictionary exists
        Document.Components!.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiExampleComponent))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            string? customName = null;
            if (a is OpenApiExampleComponent oaEa)
            {
                if (!string.IsNullOrWhiteSpace(oaEa.Key))
                {
                    customName = oaEa.Key;
                }
            }
            var name = customName ?? t.Name;
            if (!Document.Components!.Examples!.ContainsKey(name))
            {
                var ex = CreateExampleFromAttribute(a);

                var inst = Activator.CreateInstance(t);
                ex.Value ??= ToNode(inst);
                Document.Components!.Examples![name] = ex;
            }
        }
    }
    /// <summary>
    /// Adds a component example to the OpenAPI document.
    /// </summary>
    /// <param name="name">The name of the example component.</param>
    /// <param name="example">The example component to add.</param>
    /// <param name="ifExists">The conflict resolution strategy if an example with the same name already exists.</param>
    public void AddComponentExample(
        string name,
        OpenApiExample example,
        OpenApiComponentConflictResolution ifExists = OpenApiComponentConflictResolution.Overwrite)
    {
        Document.Components ??= new OpenApiComponents();
        // Ensure Examples dictionary exists
        Document.Components.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        AddComponent(Document.Components.Examples, name,
                        example, ifExists,
                        OpenApiComponentKind.Examples);
    }

    /// <summary>
    /// Creates an OpenApiExample from the specified attribute.
    /// </summary>
    /// <param name="attr">The attribute object.</param>
    /// <returns>The created OpenApiExample.</returns>
    private static OpenApiExample CreateExampleFromAttribute(object attr)
    {
        var t = attr.GetType();
        var summary = t.GetProperty("Summary")?.GetValue(attr) as string;
        var description = t.GetProperty("Description")?.GetValue(attr) as string;
        var value = t.GetProperty("Value")?.GetValue(attr);
        var external = t.GetProperty("ExternalValue")?.GetValue(attr) as string;

        var ex = new OpenApiExample
        {
            Summary = summary,
            Description = description
        };

        if (value is not null)
        {
            ex.Value = ToNode(value);
        }

        if (!string.IsNullOrWhiteSpace(external))
        {
            ex.ExternalValue = external;
        }

        return ex;
    }

    /// <summary>
    /// Applies an OpenApiExampleRefAttribute to the specified OpenApiHeader.
    /// </summary>
    /// <param name="exRef">The OpenApiExampleRefAttribute to apply.</param>
    /// <param name="header">The OpenApiHeader to which the example reference will be applied.</param>
    /// <returns>True if the example reference was successfully applied; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static bool ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiHeader header)
    {
        header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        if (header.Examples.ContainsKey(exRef.Key))
        {
            throw new InvalidOperationException($"Header already contains an example with the key '{exRef.Key}'.");
        }
        header.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
        return true;
    }

    /// <summary>
    /// Applies an OpenApiExampleAttribute to the specified OpenApiHeader.
    /// </summary>
    /// <param name="ex">The OpenApiExampleAttribute to apply.</param>
    /// <param name="header">The OpenApiHeader to which the example will be applied.</param>
    /// <returns>True if the example was successfully applied; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the example key is null or already exists in the header.</exception>
    private static bool ApplyInlineExampleAttribute(OpenApiExampleAttribute ex, OpenApiHeader header)
    {
        if (ex.Key is null)
        {
            throw new InvalidOperationException("OpenApiExampleAttribute requires a non-null Name property.");
        }
        header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        if (header.Examples.ContainsKey(ex.Key))
        {
            throw new InvalidOperationException($"Header already contains an example with the key '{ex.Key}'.");
        }
        header.Examples[ex.Key] = new OpenApiExample
        {
            Summary = ex.Summary,
            Description = ex.Description,
            Value = ToNode(ex.Value),
            ExternalValue = ex.ExternalValue
        };
        return true;
    }

    /// <summary>
    /// Tries to add an example to the given examples dictionary based on the provided attribute.
    /// </summary>
    /// <param name="examples">The dictionary of examples to add to.</param>
    /// <param name="attribute">The example attribute containing reference details.</param>
    /// <returns>True if the example was added successfully; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private bool TryAddExample(IDictionary<string, IOpenApiExample> examples, IOpenApiExampleAttribute attribute)
    {
        if (TryGetInline(name: attribute.ReferenceId, kind: OpenApiComponentKind.Examples, out OpenApiExample? example))
        {
            // If InlineComponents, clone the example
            return examples.TryAdd(attribute.Key, example!.Clone());
        }
        else if (TryGetComponent(name: attribute.ReferenceId, kind: OpenApiComponentKind.Examples, out example))
        {
            // if in main components, reference it or clone based on Inline flag
            IOpenApiExample oaExample = attribute.Inline ? example!.Clone() : new OpenApiExampleReference(attribute.ReferenceId);
            return examples.TryAdd(attribute.Key, oaExample);
        }
        else
        {
            throw new InvalidOperationException(
                $"Example with ReferenceId '{attribute.ReferenceId}' not found in components or inline components.");
        }
    }
}

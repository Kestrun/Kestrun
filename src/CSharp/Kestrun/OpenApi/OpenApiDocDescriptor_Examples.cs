using System.Collections;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
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
    /// Tries to add an example to the given examples dictionary based on the provided attribute.
    /// </summary>
    /// <param name="examples">The dictionary of examples to add to.</param>
    /// <param name="attribute">The example attribute containing reference details.</param>
    /// <returns>True if the example was added successfully; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private bool TryAddExample(IDictionary<string, IOpenApiExample>? examples, IOpenApiExampleAttribute attribute)
    {
        // If no examples dictionary, cannot add
        if (examples is null)
        {
            return false;
        }
        // Try to get the example from inline components first
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
        else if (attribute.Inline)
        {
            throw new InvalidOperationException($"Inline example component with ID '{attribute.ReferenceId}' not found.");
        }
        return false;
    }

    /// <summary>
    /// Creates a new OpenApiExample object.
    /// </summary>
    /// <param name="summary">Creates a new OpenApiExample object.</param>
    /// <param name="description">The description of the example.</param>
    /// <param name="extensions">The extensions for the example.</param>
    /// <returns>A new instance of OpenApiExample.</returns>
    private OpenApiExample NewOpenApiExample(
               string summary,
               string? description,
               IDictionary? extensions)
    {

        var example = new OpenApiExample
        {
            Summary = summary
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            example.Description = description;
        }
        // Extensions
        example.Extensions = BuildExtensions(extensions);

        return example;
    }

    /// <summary>
    /// Creates a new OpenApiExample object.
    /// </summary>
    /// <param name="summary">Creates a new OpenApiExample object.</param>
    /// <param name="description">The description of the example.</param>
    /// <param name="value">The value of the example.</param>
    /// <param name="extensions">The extensions for the example.</param>
    /// <returns>A new instance of OpenApiExample.</returns>
    public OpenApiExample NewOpenApiExample(
           string summary,
           string? description,
           object? value,
           IDictionary? extensions)
    {
        var example = NewOpenApiExample(
               summary: summary,
               description: description,
               extensions: extensions);

        // AllowNull: treat null as null JsonNode
        example.Value = OpenApiJsonNodeFactory.ToNode(value);
        // return example
        return example;
    }

    /// <summary>
    /// Creates a new OpenApiExample object. Using ExternalValue
    /// </summary>
    /// <param name="summary">Creates a new OpenApiExample object.</param>
    /// <param name="description">The description of the example.</param>
    /// <param name="externalValue">The external value of the example.</param>
    /// <param name="extensions">The extensions for the example.</param>
    /// <returns>A new instance of OpenApiExample.</returns>
    public OpenApiExample NewOpenApiExternalExample(
               string summary,
               string? description,
               string? externalValue,
               IDictionary? extensions)
    {
        var example = NewOpenApiExample(
                summary: summary,
                description: description,
                extensions: extensions);

        example.ExternalValue = externalValue;

        // return example
        return example;
    }

    /// <summary>
    /// Creates a new OpenApiExample object.
    /// </summary>
    /// <param name="summary">Creates a new OpenApiExample object.</param>
    /// <param name="description">The description of the example.</param>
    /// <param name="dataValue">The data value of the example.</param>
    /// <param name="serializedValue">The serialized value of the example.</param>
    /// <param name="extensions">The extensions for the example.</param>
    /// <returns>A new instance of OpenApiExample.</returns>
    public OpenApiExample NewOpenApiExample(
               string summary,
               string? description,
               object? dataValue,
               string? serializedValue,
               IDictionary? extensions)
    {
        var example = NewOpenApiExample(
               summary: summary,
               description: description,
               extensions: extensions);

        example.DataValue = OpenApiJsonNodeFactory.ToNode(dataValue);
        if (!string.IsNullOrWhiteSpace(serializedValue))
        {
            example.SerializedValue = serializedValue;
        }

        // return example
        return example;
    }
}

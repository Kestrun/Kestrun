
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Kestrun.Languages;

/// <summary>
/// Base class for parameter information to be injected into a script.
/// </summary>
public abstract class ParameterForInjectionInfoBase(string name, Type parameterType)
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public string Name { get; init; } = name;

    /// <summary>
    /// The .NET type of the parameter.
    /// </summary>
    public Type ParameterType { get; init; } = parameterType;

    /// <summary>
    /// The JSON schema type of the parameter.
    /// </summary>
    public JsonSchemaType? Type { get; init; }

    /// <summary>
    /// The default value of the parameter.
    /// </summary>
    public JsonNode? DefaultValue { get; init; }

    /// <summary>
    /// The location of the parameter.
    /// </summary>
    public ParameterLocation? In { get; init; }

    /// <summary>
    /// Indicates whether the parameter is from the request body.
    /// </summary>
    public bool IsRequestBody => In is null;
    /// <summary>
    /// Content types associated with the parameter.
    /// </summary>
    public List<string> ContentTypes { get; init; } = [];

    /// <summary>
    /// The style of the parameter.
    /// </summary>
    public ParameterStyle? Style { get; init; }

    /// <summary>
    /// Indicates whether the parameter should be exploded.
    /// </summary>
    public bool Explode { get; init; }
}

namespace Kestrun.OpenApi;

/// <summary>
/// Set of discovered OpenAPI component types.
/// </summary>
public sealed class OpenApiComponentSet
{
    /// <summary>
    /// Schema types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> SchemaTypes { get; init; } = [];
    /// <summary>
    /// Parameter types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> ParameterTypes { get; init; } = [];
    /// <summary>
    /// Response types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> ResponseTypes { get; init; } = [];
    /// <summary>
    /// Example types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> ExampleTypes { get; init; } = [];
    /// <summary>
    /// Request body types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> RequestBodyTypes { get; init; } = [];
    /// <summary>
    /// Header types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> HeaderTypes { get; init; } = [];
    /// <summary>
    /// Link types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> LinkTypes { get; init; } = [];
    /// <summary>
    /// Callback types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> CallbackTypes { get; init; } = [];
    /// <summary>
    /// Path item types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> PathItemTypes { get; init; } = [];

    //public IReadOnlyList<Type> ExtensionTypes { get; init; } = [];
    /// <summary>
    /// Security scheme types available for reuse in the specification.
    /// </summary>
    public IReadOnlyList<Type> SecuritySchemeTypes { get; init; } = [];
}

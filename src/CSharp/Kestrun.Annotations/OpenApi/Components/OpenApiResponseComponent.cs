/// <summary>
/// Specifies metadata for an OpenAPI Response object. Can be applied to variables
/// (properties) to contribute entries under components.responses.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiResponseComponent : OpenApiProperties
{
    /// <summary>
    /// Title is not supported for response components.
    /// </summary>
    [Obsolete("Title is not supported for response components.", error: false)]
    public new string? Title
    {
        get => base.Title;
        set => throw new NotSupportedException("Title is not supported for OpenApiResponseComponent.");
    }

    /// <summary>
    /// A brief summary of the response.
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// MIME type of the response payload (default: "application/json").
    /// </summary>
    public string[] ContentType { get; set; } = ["application/json"];

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
}

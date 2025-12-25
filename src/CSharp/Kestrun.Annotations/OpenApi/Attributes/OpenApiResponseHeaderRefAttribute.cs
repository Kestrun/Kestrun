[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OpenApiResponseHeaderRefAttribute : KestrunAnnotation, IOpenApiResponseHeaderAttribute
{
    /// <inheritdoc/>
    public required string StatusCode { get; set; }

    /// <summary>
    /// The local name under components.headers.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The reference ID under components.headers.
    /// </summary>
    public required string ReferenceId { get; set; }

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
}

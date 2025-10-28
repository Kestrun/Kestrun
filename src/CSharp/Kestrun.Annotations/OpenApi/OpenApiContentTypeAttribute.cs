/// <summary>
/// Declares a content type for an OpenAPI response or request body.
/// </summary>
/// <param name="contentType">Media type (e.g., "application/json")</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiContentTypeAttribute : Attribute
{
    /// <summary>Media type bucket (e.g., application/json, application/xml)</summary>
    public required string ContentType { get; set; }
    /// <summary>
    /// Optional reference to a predefined schema (e.g., "UserInfoResponse").
    /// </summary>
    public string? SchemaRef { get; set; }

    /// <summary>
    /// When true, embeds the schema directly instead of referencing it.
    /// </summary>
    public bool Inline { get; set; }
}

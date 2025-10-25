/// <summary>
/// Declares a content type for an OpenAPI response or request body.
/// </summary>
/// <param name="contentType">Media type (e.g., "application/json")</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiContentTypeAttribute(string contentType) : Attribute
{
    /// <summary>Media type bucket (e.g., application/json, application/xml)</summary>
    public string ContentType { get; } = contentType;
}

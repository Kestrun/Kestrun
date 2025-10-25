/// <summary>
/// Declares a reusable example on a specific media type of a response,
/// mapping a local example key to a components/examples reference.
/// </summary>
/// <param name="key">Local key under content[contentType].examples (e.g., "Default").</param>
/// <param name="refId">The components/examples id (e.g., "AddressExample").</param>
/// <param name="contentType">Media type (default "application/json").</param>
/// <remarks>
/// Create an example reference specifying the media type.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiExampleRefAttribute(string? contentType, string key, string refId) : Attribute
{
    /// <summary>Local name under content[contentType].examples</summary>
    public string Key { get; } = key;

    /// <summary>Id under #/components/examples/{RefId}</summary>
    public string RefId { get; } = refId;

    /// <summary>Media type bucket (e.g., application/json, application/xml)</summary>
    public string? ContentType { get; } = contentType;

    /// <summary>
    /// Create an example reference with the default content type of application/json.
    /// </summary>
    public OpenApiExampleRefAttribute(string key, string refId) : this(null, key, refId) { }
}

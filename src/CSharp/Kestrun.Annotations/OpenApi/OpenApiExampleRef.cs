/// <summary>
/// Declares a reusable example on a specific media type of a response,
/// mapping a local example key to a components/examples reference.
/// </summary>
/// <param name="key">Local key under content[contentType].examples (e.g., "Default").</param>
/// <param name="refId">The components/examples id (e.g., "AddressExample").</param>
/// <param name="contentType">Media type (default "application/json").</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiExampleRef : Attribute
{
    /// <summary>Local name under content[contentType].examples</summary>
    public string Key { get; }

    /// <summary>Id under #/components/examples/{RefId}</summary>
    public string RefId { get; }

    /// <summary>Media type bucket (e.g., application/json, application/xml)</summary>
    public string ContentType { get; }

    /// <summary>
    /// Create an example reference with the default content type of application/json.
    /// </summary>
    public OpenApiExampleRef(string key, string refId) : this(key, refId, "application/json") { }

    /// <summary>
    /// Create an example reference specifying the media type.
    /// </summary>
    public OpenApiExampleRef(string key, string refId, string contentType)
    {
        Key = key;
        RefId = refId;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
    }
}

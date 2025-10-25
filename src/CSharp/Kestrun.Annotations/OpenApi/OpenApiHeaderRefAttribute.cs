/// <summary>
/// Place on a property or field to indicate it is a link reference.
/// </summary>
/// <param name="key">The local name under response.links</param>
/// <param name="refId">The components/links id</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiHeaderRefAttribute(string key, string refId) : Attribute
{
    public string Key { get; } = key;     // local name under response.headers
    public string RefId { get; } = refId; // components/headers id
}

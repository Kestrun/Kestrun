/// <summary>
/// OpenAPI Schema primitive/object kinds.
/// </summary>
public enum OaSchemaType
{
    /// <summary>Let the generator infer from .NET type.</summary>
    None = 0,
    /// <summary>string</summary>
    String,
    /// <summary>number</summary>
    Number,
    /// <summary>integer</summary>
    Integer,
    /// <summary>boolean</summary>
    Boolean,
    /// <summary>array</summary>
    Array,
    /// <summary>object</summary>
    Object,
    /// <summary>null</summary>
    Null
}


/// <summary>
/// OpenAPI Schema primitive/object kinds.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public enum OaSchemaType
#pragma warning restore CA1050 // Declare types in namespaces
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
    Object
}

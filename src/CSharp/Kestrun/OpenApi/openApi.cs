// File: OpenApiAttributes.cs
// Target: .NET 8+
// Note: Placed in the GLOBAL namespace (no 'namespace { }' block) so PowerShell can use
// [OpenApiSchema] / [OpenApiParameter] without `using namespace ...`.
//
// To generate XML docs, enable in your .csproj:
//   <PropertyGroup>
//     <GenerateDocumentationFile>true</GenerateDocumentationFile>
//   </PropertyGroup>

#pragma warning disable CA1050 // Declare types in namespaces

/// <summary>
/// Kind of OpenAPI model a class represents.
/// </summary>
public enum OpenApiModelKind
{
    /// <summary>
    /// Class represents an object schema for request/response bodies.
    /// </summary>
    Schema = 0,
    /// <summary>
    /// Class represents a set of operation parameters (query/path/header/cookie).
    /// </summary>
    Parameters = 1,
    /// <summary>
    /// Class represents a reusable response object.
    /// </summary>
    Response = 2,
    /// <summary>
    /// Class represents a reusable example object.
    /// </summary>
    Example = 3,
    /// <summary>
    /// Class represents a reusable request body object.
    /// </summary>
    RequestBody = 4,
    /// <summary>
    /// Class represents a reusable header object.
    /// </summary>
    Header = 5,
    /// <summary>
    /// Class represents a reusable link object.
    /// </summary>
    Link = 6,
    /// <summary>
    /// Class represents a reusable callback object.
    /// </summary>
    Callback = 7,
    /// <summary>
    /// Class represents a reusable path item object.
    /// </summary>
    PathItem = 8,
    /// <summary>
    /// Class represents a reusable security scheme object.
    /// </summary>
    SecurityScheme = 9
}

/// <summary>
/// Marks a class as <see cref="OpenApiModelKind.Schema"/> (body) or
/// <see cref="OpenApiModelKind.Parameters"/> (operation parameters).
/// </summary>
/// <remarks>Create the attribute with a specific model kind.</remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiModelKindAttribute(OpenApiModelKind kind) : Attribute
{

    /// <summary>The kind of OpenAPI model this class represents.</summary>
    public OpenApiModelKind Kind { get; } = kind;
}


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
    Object
}

/// <summary>
/// Location of an OpenAPI parameter.
/// </summary>
public enum OaParameterLocation
{
    /// <summary>Query string parameter (?x=1)</summary>
    Query = 0,
    /// <summary>Header parameter</summary>
    Header = 1,
    /// <summary>Path parameter (/users/{id})</summary>
    Path = 2,
    /// <summary>Cookie parameter</summary>
    Cookie = 3
}


/// <summary>
/// Serialization style hints for parameters (per OAS 3.x).
/// </summary>
public enum OaParameterStyle
{
    /// <summary>Default for path &amp; header (no delimiters).</summary>
    Simple = 0,
    /// <summary>Default for query &amp; cookie.</summary>
    Form,
    /// <summary>Matrix style for path; e.g., ;color=blue.</summary>
    Matrix,
    /// <summary>Label style for path; e.g., .blue.</summary>
    Label,
    /// <summary>Space-delimited arrays in query.</summary>
    SpaceDelimited,
    /// <summary>Pipe-delimited arrays in query.</summary>
    PipeDelimited,
    /// <summary>Deep object style for nested objects in query.</summary>
    DeepObject
}

 

/// <summary>Repeat on a class to mark required property names (PowerShell-friendly).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiRequiredAttribute(string name) : Attribute
{
    /// <summary>The name of the required property.</summary>
    public string Name { get; } = name;
}

/// <summary>Place on a property to mark it as required (PowerShell-friendly).</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequiredPropertyAttribute : Attribute { }
#pragma warning restore CA1050 // Declare types in namespaces



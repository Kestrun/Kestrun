/// <summary>
/// Specifies metadata for an OpenAPI Response object. Can be applied to classes
/// to contribute entries under components.responses.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiResponseComponent : OpenApiProperties
{
#pragma warning disable IDE0051
    /// <summary>
    /// Not used. Hides Title from base class.
    /// </summary>
    private new string? Title { get; set; }
#pragma warning restore IDE0051

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

    /// <summary>
    /// Optional delimiter used by generators for naming component keys that are
    /// derived from member names (e.g., responses from properties within a class).
    /// When set and applicable (e.g., for Response kinds), generators may name
    /// a member-driven component as "ClassName{JoinClassName}MemberName" instead
    /// of just "MemberName".
    /// Example: JoinClassName = "-" => AddressResponse-OK
    /// </summary>
    public string? JoinClassName { get; set; }

}

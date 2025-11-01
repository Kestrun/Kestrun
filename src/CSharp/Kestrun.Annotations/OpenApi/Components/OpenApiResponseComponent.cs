/// <summary>
/// Specifies metadata for an OpenAPI Response object. Can be applied to classes
/// to contribute entries under components.responses.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiResponseComponent : Attribute
{
    /// <summary>
    /// Optional delimiter used by generators for naming component keys that are
    /// derived from member names (e.g., responses from properties within a class).
    /// When set and applicable (e.g., for Response kinds), generators may name
    /// a member-driven component as "ClassName{JoinClassName}MemberName" instead
    /// of just "MemberName".
    /// Example: JoinClassName = "-" => AddressResponse-OK
    /// </summary>
    public string? JoinClassName { get; set; }

    /// <summary>
    /// Optional default description for the response components.
    /// </summary>
    public string? Description { get; set; }

}

/// <summary>
/// Specifies metadata for an OpenAPI Header object. Can be applied to classes
/// to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiHeaderComponent : Attribute
{
    /// <summary>
    /// Optional delimiter used by generators for naming component keys that are
    /// derived from member names (e.g., headers from properties within a class).
    /// When set and applicable (e.g., for Header kinds), generators may name
    /// a member-driven component as "ClassName{JoinClassName}MemberName" instead
    /// of just "MemberName".
    /// Example: JoinClassName = "-" => AddressHeader-OK
    /// </summary>
    public string? JoinClassName { get; set; }

    /// <summary>
    /// Optional default description for the header components.
    /// </summary>
    public string? Description { get; set; }
}

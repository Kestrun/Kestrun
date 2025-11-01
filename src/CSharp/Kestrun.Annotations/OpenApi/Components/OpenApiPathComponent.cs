/// <summary>
/// Specifies metadata for an OpenAPI Parameter object. Can be applied to classes
/// to contribute entries under components.parameters.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiPathComponent : Attribute
{
    /// <summary>
    /// Optional delimiter used by generators for naming component keys that are
    /// derived from member names (e.g., parameters from properties within a class).
    /// When set and applicable (e.g., for Parameter kinds), generators may name
    /// a member-driven component as "ClassName{JoinClassName}MemberName" instead
    /// of just "MemberName".
    /// Example: JoinClassName = "-" => AddressParameter-OK
    /// </summary>
    public string? JoinClassName { get; set; }

    /// <summary>
    /// Optional default description for the parameter components.
    /// </summary>
    public string? Description { get; set; }

}

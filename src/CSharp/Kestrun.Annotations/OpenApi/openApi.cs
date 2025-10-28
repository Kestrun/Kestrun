/// <summary>
/// Marks a class as <see cref="OpenApiModelKind.Schema"/> (body) or
/// <see cref="OpenApiModelKind.Parameters"/> (operation parameters).
/// </summary>
/// <param name="kind">The kind of OpenAPI model.</param>
/// <remarks>Create the attribute with a specific model kind.</remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiModelKindAttribute(OpenApiModelKind kind) : Attribute
{

    /// <summary>The kind of OpenAPI model this class represents.</summary>
    public OpenApiModelKind Kind { get; } = kind;

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
    /// For class-first generators (e.g., RequestBody), when true instructs the generator to
    /// emit an inline schema in-place rather than a $ref to components.schemas.
    /// </summary>
    public bool Inline { get; set; }
}

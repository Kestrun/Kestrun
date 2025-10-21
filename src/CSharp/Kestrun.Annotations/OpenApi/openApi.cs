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
/// Marks a class as <see cref="OpenApiModelKind.Schema"/> (body) or
/// <see cref="OpenApiModelKind.Parameters"/> (operation parameters).
/// </summary>
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
    public bool InlineSchema { get; set; }
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

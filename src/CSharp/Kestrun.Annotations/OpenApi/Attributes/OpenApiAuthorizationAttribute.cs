/// <summary>
/// OpenAPI Parameter metadata for query/path/header/cookie items.
/// Apply on properties inside a class marked with <see cref="OpenApiModelKindAttribute"/> = Parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OpenApiAuthorizationAttribute : KestrunAnnotation
{
    // TODO: Implement reader for this attribute in OpenApi generator
    /// <summary>
    /// The name of the security scheme to apply.
    /// </summary>
    public string? Scheme { get; set; }

    /// <summary>
    /// Optional description for the authentication requirement.
    /// Comma-separated list of policies to apply.
    /// </summary>
    public string? Policies { get; set; }
}

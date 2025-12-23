
/// <summary>
/// Rich OpenAPI schema metadata for a property or a class.
/// Apply to:
/// <list type="bullet">
/// <item><description>Class (object-level): set <see cref="Required"/> array, XML hints, discriminator, etc.</description></item>
/// <item><description>Property (member-level): set description, format, constraints, enum, etc.</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiPropertyAttribute : OpenApiProperties
{
    /// <summary>
    /// The HTTP status code (e.g., "200", "400", "404").
    /// This is only used when applied to method parameters to
    /// associate the property with a specific response.
    /// </summary>
    public string? StatusCode { get; set; }
}

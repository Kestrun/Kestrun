
/// <summary>
/// Rich OpenAPI schema metadata for a class.
/// Apply to:
/// <list type="bullet">
/// <item><description>Class (object-level): configure pattern-based property schemas (e.g., set <see cref="Required"/> array, XML hints, discriminator, etc.).</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiPatternPropertiesAttribute : OpenApiProperties
{
    /// <summary>
    /// ECMA-262 compliant regex pattern for property names.
    /// </summary>
    public required string KeyPattern { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiPatternPropertiesAttribute"/> class.
    /// </summary>
    public OpenApiPatternPropertiesAttribute() => SchemaType = typeof(string);
}

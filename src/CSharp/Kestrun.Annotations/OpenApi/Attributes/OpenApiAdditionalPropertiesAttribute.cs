
/// <summary>
/// Rich OpenAPI schema metadata for a property or a class.
/// Apply to:
/// <list type="bullet">
/// <item><description>Class (object-level): set <see cref="Required"/> array, XML hints, discriminator, etc.</description></item>
/// <item><description>Property (member-level): set description, format, constraints, enum, etc.</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field, Inherited = true)]
public sealed class OpenApiAdditionalPropertiesAttribute : OpenApiProperties { }

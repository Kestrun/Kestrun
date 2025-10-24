/// <summary>
/// Place on a class to specify required property names for OpenAPI models.
/// </summary>
/// <param name="name">The name of the required property.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiRequiredAttribute(string name) : Attribute
{
    /// <summary>
    /// The name of the required property.
    /// </summary>
    public string Name { get; } = name;
}

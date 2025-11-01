/// <summary>
/// Place on a property or field to indicate it is a server variable.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiServerVariableAttribute : Attribute
{
    /// <summary>The placeholder name (e.g., "region" for {region}).</summary>
    public string? Name { get; set; }

    /// <summary>Default value for the variable.</summary>
    public string? Default { get; set; }

    /// <summary>Optional allowed values.</summary>
    public string[]? Enum { get; set; }

    /// <summary>Description of the variable.</summary>
    public string? Description { get; set; }
}

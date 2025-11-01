/// <summary>
/// Place on a property or field to indicate it is a server.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiServerAttribute : Attribute
{
    public string? Url { get; set; }
    public string? Description { get; set; }
}

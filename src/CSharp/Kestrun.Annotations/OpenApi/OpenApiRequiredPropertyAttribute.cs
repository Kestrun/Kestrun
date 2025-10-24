/// <summary>
/// Place on a property to mark it as required (PowerShell-friendly).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequiredPropertyAttribute : Attribute { }

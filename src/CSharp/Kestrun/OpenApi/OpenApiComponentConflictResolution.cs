namespace Kestrun.OpenApi;

/// <summary>
/// Defines conflict resolution strategies when adding OpenAPI components.
/// </summary>
public enum OpenApiComponentConflictResolution
{
    /// <summary>
    /// Throws an error if a component with the same name already exists.
    /// </summary>
    Error,
    /// <summary>
    /// Overwrites the existing component if a component with the same name already exists.
    /// </summary>
    Overwrite,
    /// <summary>
    /// Ignores the new component if a component with the same name already exists.
    /// </summary>
    Ignore
}

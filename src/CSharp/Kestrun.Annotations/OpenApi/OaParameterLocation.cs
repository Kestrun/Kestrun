/// <summary>
/// Location of an OpenAPI parameter.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public enum OaParameterLocation
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>Query string parameter (?x=1)</summary>
    Query = 0,
    /// <summary>Header parameter</summary>
    Header = 1,
    /// <summary>Path parameter (/users/{id})</summary>
    Path = 2,
    /// <summary>Cookie parameter</summary>
    Cookie = 3
}

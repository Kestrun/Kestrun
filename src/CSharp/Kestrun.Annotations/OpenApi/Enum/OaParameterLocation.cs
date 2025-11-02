/// <summary>
/// Location of an OpenAPI parameter.
/// </summary>
public enum OaParameterLocation
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

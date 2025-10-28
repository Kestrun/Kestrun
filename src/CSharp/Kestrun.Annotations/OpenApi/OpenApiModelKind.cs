
/// <summary>
/// Kind of OpenAPI model a class represents.
/// </summary> 
public enum OpenApiModelKind
{
    /// <summary>
    /// Class represents an object schema for request/response bodies.
    /// </summary>
    Schema = 0,
    /// <summary>
    /// Class represents a set of operation parameters (query/path/header/cookie).
    /// </summary>
    Parameters = 1,
    /// <summary>
    /// Class represents a reusable response object.
    /// </summary>
    Response = 2,
    /// <summary>
    /// Class represents a reusable example object.
    /// </summary>
    Example = 3,
    /// <summary>
    /// Class represents a reusable request body object.
    /// </summary>
    RequestBody = 4,
    /// <summary>
    /// Class represents a reusable header object.
    /// </summary>
    Header = 5,
    /// <summary>
    /// Class represents a reusable link object.
    /// </summary>
    Link = 6,
    /// <summary>
    /// Class represents a reusable callback object.
    /// </summary>
    Callback = 7,
    /// <summary>
    /// Class represents a reusable path item object.
    /// </summary>
    PathItem = 8,
    /// <summary>
    /// Class represents a reusable security scheme object.
    /// </summary>
    SecurityScheme = 9
}

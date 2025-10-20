namespace Kestrun.OpenApi;

/// <summary>
/// Specifies metadata for an OpenAPI response object.
/// Can be attached to PowerShell or C# classes representing reusable responses.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiResponseAttribute : Attribute
{
    /// <summary>
    /// The HTTP status code (e.g., "200", "400", "404").
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// A short description of the response (required in OpenAPI spec).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional reference to a predefined schema (e.g., "UserInfoResponse").
    /// </summary>
    public string? SchemaRef { get; set; }

    /// <summary>
    /// MIME type of the response payload (default: "application/json").
    /// </summary>
    public string? ContentType { get; set; } = "application/json";

    /// <summary>
    /// Optional reference to an example component (e.g., "ExampleUser").
    /// </summary>
    public string? ExampleRef { get; set; }

    /// <summary>
    /// When true, marks the response as a default response (instead of a specific HTTP status).
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Optional header reference name, if the response defines a custom header.
    /// </summary>
    public string? HeaderRef { get; set; }

    /// <summary>
    /// Optional link reference (for response follow-ups, e.g. HATEOAS).
    /// </summary>
    public string? LinkRef { get; set; }

    /// <summary>
    /// Optional summary for documentation purposes.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Optional deprecation flag.
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public OpenApiResponseAttribute() { }

    /// <summary>
    /// Constructor with most common arguments.
    /// </summary>
    /// <param name="status">HTTP status code (e.g. "200")</param>
    /// <param name="description">Response description</param>
    public OpenApiResponseAttribute(string status, string description)
    {
        Status = status;
        Description = description;
    }
}
